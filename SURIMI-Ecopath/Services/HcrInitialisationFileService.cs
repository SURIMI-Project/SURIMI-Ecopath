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
    // FIFO eviction retains only the latest 12 spin-up timesteps for the initial_catch.csv annual total.
    private readonly Queue<Dictionary<SpeciesKey, double>> _catchHistory = new();

    private readonly record struct SpeciesKey(string SpeciesCode, string LengthClass, string Age, string LifeStage);
    private sealed record BiomassCsvRow(string SpeciesCode, string LengthClass, string Age, string LifeStage, double BiomassKg);
    private sealed record CatchCsvRow(string SpeciesCode, string LengthClass, string Age, string LifeStage, double Catch);

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
        Enabled = Environment.GetEnvironmentVariable(CreateFilesEnvironmentVariable) is not null;
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
        // Spin-up can span many years, but initial_catch.csv needs only the final 12 modeled timesteps.
        // Remove the oldest snapshot after adding a new one so earlier spin-up catches cannot inflate the total.
        while (_catchHistory.Count > CatchHistoryLength)
            _catchHistory.Dequeue();
    }

    /// <inheritdoc />
    public async Task WriteInitialFilesAsync(Biomass biomass, CancellationToken cancellationToken)
    {
        if (!Enabled)
            throw new InvalidOperationException("HCR initialization files are not enabled.");
        if (_catchHistory.Count < CatchHistoryLength)
            throw new InvalidOperationException($"Cannot create initial_catch.csv: {_catchHistory.Count} modeled timestep(s) are available, but {CatchHistoryLength} are required.");
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

        var orderedSpecies = biomassBySpecies.Keys
            .OrderBy(species => species.SpeciesCode, StringComparer.Ordinal)
            .ThenBy(species => species.LengthClass, StringComparer.Ordinal)
            .ThenBy(species => species.Age, StringComparer.Ordinal)
            .ThenBy(species => species.LifeStage, StringComparer.Ordinal)
            .ToList();
        var biomassRows = orderedSpecies
            .Select(species => new BiomassCsvRow(
                species.SpeciesCode,
                species.LengthClass,
                species.Age,
                species.LifeStage,
                biomassBySpecies[species]))
            .ToList();
        var catchRows = orderedSpecies
            .Select(species => new CatchCsvRow(
                species.SpeciesCode,
                species.LengthClass,
                species.Age,
                species.LifeStage,
                catchBySpecies.GetValueOrDefault(species)))
            .ToList();

        var files = new Dictionary<string, string>
        {
            ["initial_biomasses.csv"] = SerializeCsv(biomassRows),
            ["initial_catch.csv"] = SerializeCsv(catchRows)
        };
        var attemptedFiles = new List<string>();
        try
        {
            foreach (var (fileName, csvContent) in files)
            {
                attemptedFiles.Add(fileName);
                using var content = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
                await _blobStore.UploadAsync(fileName, PathType.Output, content, "text/csv", new Dictionary<string, string>(), cancellationToken);
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

        _logger.LogInformation("Wrote HCR initialization files for {SpeciesCount} species using {CatchTimestepCount} catch timesteps", biomassRows.Count, _catchHistory.Count);
    }

    private static SpeciesKey ToSpeciesKey(Species species)
    {
        return new SpeciesKey(
            species.SpeciesCode ?? string.Empty,
            species.LengthClass ?? string.Empty,
            species.Age ?? string.Empty,
            species.LifeStage ?? string.Empty);
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
