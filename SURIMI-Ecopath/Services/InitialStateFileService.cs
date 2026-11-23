using System.Globalization;
using CsvHelper;
using Eii.BlobStore;

namespace Ecopath.Services;

/// <inheritdoc />
public sealed class InitialStateFileService : IInitialStateFileService
{
    private readonly IBlobStore m_blobStore;
    private readonly ILogger<InitialStateFileService> m_logger;

    public InitialStateFileService(IBlobStore blobStore, ILogger<InitialStateFileService> logger)
    {
        m_blobStore = blobStore;
        m_logger = logger;
    }

    /// <inheritdoc />
    public async Task<Dictionary<IInitialStateFileService.SpeciesKey, IInitialStateFileService.InitialHcrData>> ReadInitialHCRDataAsync(string scenarioName, CancellationToken cancellationToken)
    {
        var values = await ReadValuesAsync(scenarioName, cancellationToken);
        var initialHCRData = new Dictionary<IInitialStateFileService.SpeciesKey, IInitialStateFileService.InitialHcrData>();
        foreach (var (species, valuesForSpecies) in values)
        {
            double biomass = valuesForSpecies.Biomass;
            if (biomass == 0)
                throw new InvalidDataException($"Cannot calculate initial fishing mortality for '{species}': initial biomass is zero.");

            double mortality = valuesForSpecies.Catch / biomass;
            if (!double.IsFinite(mortality))
                throw new InvalidDataException($"Initial fishing mortality for '{species}' is not finite.");
            initialHCRData.Add(species, new IInitialStateFileService.InitialHcrData(biomass, mortality, valuesForSpecies.BiomassLimit, valuesForSpecies.BiomassBase, valuesForSpecies.FMax));
        }
        return initialHCRData;
    }

    private readonly record struct FileValues(double Biomass, double Catch, double BiomassLimit, double BiomassBase, double FMax);

    private async Task<Dictionary<IInitialStateFileService.SpeciesKey, FileValues>> ReadValuesAsync(string scenarioName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scenarioName) || scenarioName is "." or ".." ||
            scenarioName.IndexOfAny(['/', '\\', ':']) >= 0 || Path.IsPathRooted(scenarioName))
            throw new ArgumentException("ScenarioName must be a single non-empty subdirectory name.", nameof(scenarioName));

        // Blob keys use slash separators on every platform, including Windows.
        string path = string.Join('/', scenarioName, IInitialStateFileService.FileName(scenarioName));
        m_logger.LogInformation("Reading initial state from {BlobPath}", path);
        using var stream = await m_blobStore.OpenReadAsync(path, PathType.Input, cancellationToken);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        if (!await csv.ReadAsync())
            throw new InvalidDataException($"Initial state file '{path}' is empty.");
        csv.ReadHeader();
        string[] requiredColumns = ["SpeciesCode", "LengthClass", "Age", "LifeStage", "Biomass", "Catch"];
        if (requiredColumns.Any(column => !csv.HeaderRecord!.Contains(column, StringComparer.Ordinal)))
            throw new InvalidDataException($"Initial state file '{path}' must contain columns {string.Join(",", requiredColumns)}.");

        var values = new Dictionary<IInitialStateFileService.SpeciesKey, FileValues>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var species = new IInitialStateFileService.SpeciesKey(
                csv.GetField("SpeciesCode")!,
                csv.GetField("LengthClass")!,
                csv.GetField("Age")!,
                csv.GetField("LifeStage")!);
            double biomass = csv.GetField<double>("Biomass");
            if (string.IsNullOrWhiteSpace(species.SpeciesCode) || !double.IsFinite(biomass) || biomass < 0)
                throw new InvalidDataException($"Invalid species or Biomass in '{path}' at row {csv.Parser.Row}.");
            if (!csv.TryGetField("Catch", out double caught) || !double.IsFinite(caught) || caught < 0)
                throw new InvalidDataException($"Missing or invalid Catch in '{path}' at row {csv.Parser.Row}.");
            double biomassLimit = ReadOptionalReferenceLevel(csv, "BiomassLimit", path);
            double biomassBase = ReadOptionalReferenceLevel(csv, "BiomassBase", path);
            double fMax = ReadOptionalReferenceLevel(csv, "FMax", path);
            if (!values.TryAdd(species, new FileValues(biomass, caught, biomassLimit, biomassBase, fMax)))
                throw new InvalidDataException($"Duplicate species '{species}' in '{path}'.");
        }
        if (values.Count == 0)
            throw new InvalidDataException($"Initial state file '{path}' contains no species rows.");
        return values;
    }

    // Reference levels are optional so that files written before they were exported remain readable; absent or blank values read as zero.
    private static double ReadOptionalReferenceLevel(CsvReader csv, string column, string path)
    {
        if (!csv.HeaderRecord!.Contains(column, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(csv.GetField(column)))
            return 0;
        if (!csv.TryGetField(column, out double value) || !double.IsFinite(value) || value < 0)
            throw new InvalidDataException($"Invalid {column} in '{path}' at row {csv.Parser.Row}.");
        return value;
    }
}
