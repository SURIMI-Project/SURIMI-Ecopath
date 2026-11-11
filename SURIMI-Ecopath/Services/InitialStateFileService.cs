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
    public Task<Dictionary<IInitialStateFileService.SpeciesKey, double>> ReadBiomassesAsync(string scenarioName, CancellationToken cancellationToken)
    {
        return ReadValuesAsync(scenarioName, "initial_biomasses.csv", "Biomass", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<IInitialStateFileService.SpeciesKey, double>> ReadFishingMortalitiesAsync(string scenarioName, CancellationToken cancellationToken)
    {
        var biomasses = await ReadBiomassesAsync(scenarioName, cancellationToken);
        var catches = await ReadValuesAsync(scenarioName, "initial_catch.csv", "Catch", cancellationToken);
        if (!biomasses.Keys.ToHashSet().SetEquals(catches.Keys))
            throw new InvalidDataException($"Initial biomass and catch files for scenario '{scenarioName}' must contain the same species keys.");

        var mortalities = new Dictionary<IInitialStateFileService.SpeciesKey, double>();
        foreach (var (species, biomass) in biomasses)
        {
            if (biomass == 0)
                throw new InvalidDataException($"Cannot calculate initial fishing mortality for '{species}': initial biomass is zero.");

            // Initial fishing mortality is the species' annual initial catch divided by its initial biomass.
            double mortality = catches[species] / biomass;
            if (!double.IsFinite(mortality))
                throw new InvalidDataException($"Initial fishing mortality for '{species}' is not finite.");
            mortalities.Add(species, mortality);
        }
        return mortalities;
    }

    private async Task<Dictionary<IInitialStateFileService.SpeciesKey, double>> ReadValuesAsync(string scenarioName, string fileName, string valueColumn, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scenarioName) || scenarioName is "." or ".." ||
            scenarioName.IndexOfAny(['/', '\\', ':']) >= 0 || Path.IsPathRooted(scenarioName))
            throw new ArgumentException("ScenarioName must be a single non-empty subdirectory name.", nameof(scenarioName));

        // Blob keys use slash separators on every platform, including Windows.
        string path = string.Join('/', scenarioName, fileName);
        m_logger.LogInformation("Reading initial state from {BlobPath}", path);
        using var stream = await m_blobStore.OpenReadAsync(path, PathType.Input, cancellationToken);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        if (!await csv.ReadAsync())
            throw new InvalidDataException($"Initial state file '{path}' is empty.");
        csv.ReadHeader();
        string[] requiredColumns = ["SpeciesCode", "LengthClass", "Age", "LifeStage", valueColumn];
        if (requiredColumns.Any(column => !csv.HeaderRecord!.Contains(column, StringComparer.Ordinal)))
            throw new InvalidDataException($"Initial state file '{path}' must contain columns {string.Join(",", requiredColumns)}.");

        var values = new Dictionary<IInitialStateFileService.SpeciesKey, double>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var species = new IInitialStateFileService.SpeciesKey(
                csv.GetField("SpeciesCode")!,
                csv.GetField("LengthClass")!,
                csv.GetField("Age")!,
                csv.GetField("LifeStage")!);
            double value = csv.GetField<double>(valueColumn);
            if (string.IsNullOrWhiteSpace(species.SpeciesCode) || !double.IsFinite(value) || value < 0)
                throw new InvalidDataException($"Invalid species or {valueColumn} in '{path}' at row {csv.Parser.Row}.");
            if (!values.TryAdd(species, value))
                throw new InvalidDataException($"Duplicate species '{species}' in '{path}'.");
        }
        if (values.Count == 0)
            throw new InvalidDataException($"Initial state file '{path}' contains no species rows.");
        return values;
    }
}
