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
    public async Task<Dictionary<IInitialStateFileService.SpeciesKey, (double Biomass, double FishingMortality)>> ReadInitialHCRDataAsync(string scenarioName, CancellationToken cancellationToken)
    {
        var values = await ReadValuesAsync(scenarioName, cancellationToken);
        var initialHCRData = new Dictionary<IInitialStateFileService.SpeciesKey, (double Biomass, double FishingMortality)>();
        foreach (var (species, valuesForSpecies) in values)
        {
            double biomass = valuesForSpecies.Biomass;
            if (biomass == 0)
                throw new InvalidDataException($"Cannot calculate initial fishing mortality for '{species}': initial biomass is zero.");

            double mortality = valuesForSpecies.Catch / biomass;
            if (!double.IsFinite(mortality))
                throw new InvalidDataException($"Initial fishing mortality for '{species}' is not finite.");
            initialHCRData.Add(species, (biomass, mortality));
        }
        return initialHCRData;
    }

    /// <inheritdoc />
    public async Task<Dictionary<IInitialStateFileService.SpeciesKey, double>> ReadFishingMortalitiesAsync(string scenarioName, CancellationToken cancellationToken)
    {
        var initialHCRData = await ReadInitialHCRDataAsync(scenarioName, cancellationToken);
        return initialHCRData.ToDictionary(entry => entry.Key, entry => entry.Value.FishingMortality);
    }

    private async Task<Dictionary<IInitialStateFileService.SpeciesKey, (double Biomass, double Catch)>> ReadValuesAsync(string scenarioName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scenarioName) || scenarioName is "." or ".." ||
            scenarioName.IndexOfAny(['/', '\\', ':']) >= 0 || Path.IsPathRooted(scenarioName))
            throw new ArgumentException("ScenarioName must be a single non-empty subdirectory name.", nameof(scenarioName));

        // Blob keys use slash separators on every platform, including Windows.
        string path = string.Join('/', scenarioName, "InitialHCRData.csv");
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

        var values = new Dictionary<IInitialStateFileService.SpeciesKey, (double Biomass, double Catch)>();
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
            if (!values.TryAdd(species, (biomass, caught)))
                throw new InvalidDataException($"Duplicate species '{species}' in '{path}'.");
        }
        if (values.Count == 0)
            throw new InvalidDataException($"Initial state file '{path}' contains no species rows.");
        return values;
    }
}
