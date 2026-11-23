using System.Globalization;
using System.Text;
using CsvHelper;
using Eii.BlobStore;
using SURIMI.Datamodel;

namespace Ecopath.Services;

/// <inheritdoc />
public class HcrInitialisationFileService : IHcrInitialisationFileService
{
    private const int CatchHistoryLength = 12;
    private const string CreateFilesEnvironmentVariable = "CREATE_HCR_INITIALISE_FILES";
    private readonly IBlobStore _blobStore;
    private readonly ILogger<HcrInitialisationFileService> _logger;
    // Each entry holds one completed spin-up month's gross catch totals by species, summed across cells and fleets.
    // FIFO eviction retains only the latest 12 spin-up timesteps for the annual catch total.
    private readonly Queue<Dictionary<SpeciesKey, double>> _catchHistory = new();

    private readonly record struct SpeciesKey(string SpeciesCode, string LengthClass, string Age, string LifeStage);
    private sealed record InitialHcrCsvRow(string SpeciesCode, string LengthClass, string Age, string LifeStage, double Biomass, double Catch, double BiomassLimit, double BiomassBase, double FMax);

    public HcrInitialisationFileService(IBlobStore blobStore, ILogger<HcrInitialisationFileService> logger)
    {
        _blobStore = blobStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool Enabled { get; private set; }

    /// <inheritdoc />
    public void BeginInitialization()
    {
        string? setting = Environment.GetEnvironmentVariable(CreateFilesEnvironmentVariable);
        Enabled = setting == "1" || (bool.TryParse(setting, out bool enabled) && enabled);
        _catchHistory.Clear();
    }

    /// <inheritdoc />
    public void CaptureCatchTimestep(CatchDispositionSummary catchSummary)
    {
        if (!Enabled)
            return;

        var timestepCatch = new Dictionary<SpeciesKey, double>();
        foreach (var grid in catchSummary.DispositionGrids)
        {
            SpeciesKey species = ToSpeciesKey(grid.Species);
            // Gross catch includes dead discards; DeadDiscardsBiomass is its subset.
            double caught = grid.DispositionCells.Sum(cell => cell.GrossCatchBiomass);
            timestepCatch.TryGetValue(species, out double currentTotal);
            timestepCatch[species] = currentTotal + caught;
        }

        _catchHistory.Enqueue(timestepCatch);
        // Remove older spin-up catches so only the final 12 modeled timesteps contribute to the total.
        while (_catchHistory.Count > CatchHistoryLength)
            _catchHistory.Dequeue();
    }

    /// <inheritdoc />
    public async Task WriteRecruitmentFileAsync(IReadOnlyCollection<MseRecruitment> recruitment, string scenarioName, CancellationToken cancellationToken)
    {
        if (!Enabled)
            throw new InvalidOperationException("HCR initialization files are not enabled.");

        string fileName = $"{scenarioName}_recruitment.csv";
        await WriteOutputFilesAsync(scenarioName, [(fileName, SerializeRecruitmentCsv(recruitment))], cancellationToken);
        _logger.LogInformation("Wrote stock recruitment file {FileName} for scenario {ScenarioName}", fileName, scenarioName);
    }

    /// <inheritdoc />
    public async Task WriteQuotaShareFileAsync(IReadOnlyCollection<MseQuotaShare> quotaShares, string scenarioName, CancellationToken cancellationToken)
    {
        if (!Enabled)
            throw new InvalidOperationException("HCR initialization files are not enabled.");

        var rows = new List<MseQuotaShare>();
        foreach (var stock in quotaShares.GroupBy(row => (row.SpeciesCode, row.LifeStage)))
        {
            var row = stock.First();
            double sum = row.Shares.Values.Sum();
            // Match the fisheries authority's tolerance without redistributing any of the model's shares.
            if (Math.Abs(sum - 1) > 1e-4 || row.Shares.Values.Any(share => !double.IsFinite(share) || share < 0 || share > 1))
            {
                _logger.LogWarning("Skipping quota shares for {SpeciesCode} ({LifeStage}): expected valid shares summing to 1, found total {ShareSum}", row.SpeciesCode, row.LifeStage, sum);
                continue;
            }
            if (stock.Skip(1).Any(other => other.Shares.Count != row.Shares.Count ||
                row.Shares.Any(share => !other.Shares.TryGetValue(share.Key, out double value) || value != share.Value)))
            {
                _logger.LogWarning("Skipping conflicting quota shares for {SpeciesCode} ({LifeStage})", row.SpeciesCode, row.LifeStage);
                continue;
            }
            rows.Add(row);
        }

        if (rows.Count == 0)
        {
            _logger.LogWarning("No valid mapped stock and fleet quota shares to export for scenario {ScenarioName}", scenarioName);
            return;
        }

        string fileName = $"{scenarioName}_quotashare.csv";
        await WriteOutputFilesAsync(scenarioName, [(fileName, SerializeQuotaShareCsv(rows))], cancellationToken);
        _logger.LogInformation("Wrote fleet quota shares for {StockCount} stocks to {FileName}", rows.Count, fileName);
    }

    /// <inheritdoc />
    public async Task WriteInitialFilesAsync(Biomass biomass, IReadOnlyCollection<DefaultHarvestControlRules> defaultHarvestControlRules, string scenarioName, CancellationToken cancellationToken)
    {
        if (!Enabled)
            throw new InvalidOperationException("HCR initialization files are not enabled.");
        if (_catchHistory.Count < CatchHistoryLength)
            throw new InvalidOperationException($"Cannot create {IInitialStateFileService.FileName(scenarioName)}: {_catchHistory.Count} modeled timestep(s) are available, but {CatchHistoryLength} are required.");
        if (biomass.BiomassGrids == null)
            throw new InvalidOperationException("Initial biomass snapshot is not available.");

        var biomassBySpecies = new Dictionary<SpeciesKey, double>();
        foreach (var grid in biomass.BiomassGrids)
        {
            SpeciesKey species = ToSpeciesKey(grid.Species);
            biomassBySpecies.TryGetValue(species, out double currentBiomass);
            biomassBySpecies[species] = currentBiomass + (grid.BiomassCells?.Sum(cell => cell.Biomass) ?? 0);
        }

        var catchBySpecies = new Dictionary<SpeciesKey, double>();
        foreach (var timestepCatch in _catchHistory)
        {
            foreach (var (species, caught) in timestepCatch)
            {
                catchBySpecies.TryGetValue(species, out double currentCatch);
                catchBySpecies[species] = currentCatch + caught;
            }
        }

        var levelsBySpecies = new Dictionary<SpeciesKey, DefaultHarvestControlRules>();
        foreach (var levels in defaultHarvestControlRules)
            levelsBySpecies[new SpeciesKey(levels.SpeciesCode, levels.LengthClass, levels.Age, levels.LifeStage)] = levels;

        var orderedSpecies = biomassBySpecies.Keys
            .OrderBy(species => species.SpeciesCode, StringComparer.Ordinal)
            .ThenBy(species => species.LengthClass, StringComparer.Ordinal)
            .ThenBy(species => species.Age, StringComparer.Ordinal)
            .ThenBy(species => species.LifeStage, StringComparer.Ordinal)
            .ToList();
        var rows = orderedSpecies
            .Select(species =>
            {
                levelsBySpecies.TryGetValue(species, out var levels);
                return new InitialHcrCsvRow(
                    species.SpeciesCode,
                    species.LengthClass,
                    species.Age,
                    species.LifeStage,
                    biomassBySpecies[species],
                    catchBySpecies.GetValueOrDefault(species),
                    levels?.BiomassLimit ?? 0,
                    levels?.BiomassBase ?? 0,
                    levels?.FMax ?? 0);
            })
            .ToList();

        await WriteOutputFilesAsync(scenarioName, [(IInitialStateFileService.FileName(scenarioName), SerializeCsv(rows))], cancellationToken);
        _logger.LogInformation("Wrote HCR initialization data for {SpeciesCount} species using {CatchTimestepCount} catch timesteps", rows.Count, _catchHistory.Count);
    }

    private async Task WriteOutputFilesAsync(string scenarioName, IReadOnlyList<(string FileName, string CsvContent)> files, CancellationToken cancellationToken)
    {
        var attemptedFiles = new List<string>();
        try
        {
            foreach (var (fileName, csvContent) in files)
            {
                string blobKey = string.Join('/', scenarioName, fileName);
                attemptedFiles.Add(blobKey);
                using var content = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
                await _blobStore.UploadAsync(blobKey, PathType.Output, content, "text/csv", new Dictionary<string, string>(), cancellationToken);
            }
        }
        catch
        {
            foreach (string fileName in attemptedFiles)
            {
                try
                {
                    await _blobStore.DeleteAsync(fileName, PathType.Output, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogError(cleanupException, "Failed to remove incomplete HCR initialization file {FileName}", fileName);
                }
            }
            throw;
        }
    }

    private static SpeciesKey ToSpeciesKey(Species species)
    {
        return new SpeciesKey(
            species.SpeciesCode ?? string.Empty,
            species.LengthClass ?? string.Empty,
            species.Age ?? string.Empty,
            species.LifeStage ?? string.Empty);
    }

    private static string SerializeRecruitmentCsv(IEnumerable<MseRecruitment> recruitment)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var header in new[] { "species_code", "life_stage", "RstockRatio", "RHalfB0Ratio", "cvRec" })
            csv.WriteField(header);
        csv.NextRecord();

        // The fisheries authority keys recruitment by species and life stage only, ignoring case.
        var stocks = recruitment
            .GroupBy(row => (SpeciesCode: row.SpeciesCode.ToUpperInvariant(), LifeStage: row.LifeStage.ToUpperInvariant()))
            .OrderBy(stock => stock.Key.SpeciesCode, StringComparer.Ordinal)
            .ThenBy(stock => stock.Key.LifeStage, StringComparer.Ordinal);
        foreach (var stock in stocks)
        {
            var values = stock.Select(row => (row.RstockRatio, row.RHalfB0Ratio, row.CvRec)).Distinct().ToList();
            if (values.Count != 1)
                throw new InvalidDataException($"Conflicting MSE recruitment parameters for species '{stock.Key.SpeciesCode}' and life stage '{stock.Key.LifeStage}'.");

            csv.WriteField(stock.Key.SpeciesCode);
            csv.WriteField(stock.Key.LifeStage);
            csv.WriteField(values[0].RstockRatio);
            csv.WriteField(values[0].RHalfB0Ratio);
            csv.WriteField(values[0].CvRec);
            csv.NextRecord();
        }
        csv.Flush();
        return writer.ToString();
    }

    private static string SerializeQuotaShareCsv(IReadOnlyCollection<MseQuotaShare> rows)
    {
        var fleets = rows.SelectMany(row => row.Shares.Keys).Distinct()
            .OrderBy(fleet => fleet.GearCode, StringComparer.Ordinal)
            .ThenBy(fleet => fleet.CountryCode, StringComparer.Ordinal)
            .ToArray();

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteField("species_code");
        csv.WriteField("life_stage");
        foreach (var fleet in fleets)
            csv.WriteField($"{fleet.GearCode}|{fleet.CountryCode}");
        csv.NextRecord();

        foreach (var row in rows.OrderBy(row => row.SpeciesCode, StringComparer.Ordinal).ThenBy(row => row.LifeStage, StringComparer.Ordinal))
        {
            csv.WriteField(row.SpeciesCode);
            csv.WriteField(row.LifeStage);
            foreach (var fleet in fleets)
                csv.WriteField(row.Shares.GetValueOrDefault(fleet).ToString("G9", CultureInfo.InvariantCulture));
            csv.NextRecord();
        }
        csv.Flush();
        return writer.ToString();
    }

    private static string SerializeCsv<T>(IEnumerable<T> rows)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(rows);
        csv.Flush();
        return writer.ToString();
    }
}
