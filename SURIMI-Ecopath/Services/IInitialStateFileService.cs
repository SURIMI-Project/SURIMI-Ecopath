namespace Ecopath.Services;

/// <summary>
/// Reads initial biomass and fishing mortality data from scenario input files.
/// </summary>
/// <remarks>
/// This service uses the application's shared blob store. Call these methods before simulation
/// initialization changes the blob store's active scenario subdirectory.
/// </remarks>
public interface IInitialStateFileService
{
    /// <summary>
    /// Identifies a species using its complete species, length-class, age, and life-stage key.
    /// </summary>
    public readonly record struct SpeciesKey(string SpeciesCode, string LengthClass, string Age, string LifeStage);

    /// <summary>
    /// Gets the name of the initial HCR data file for a scenario.
    /// </summary>
    public static string FileName(string scenarioName) => $"{scenarioName}_initial_hrc_data.csv";

    /// <summary>
    /// Initial harvest control rule data for one species.
    /// </summary>
    public readonly record struct InitialHcrData(double Biomass, double FishingMortality, double DefaultBiomassLimit, double DefaultBiomassBase, double DefaultFMax);

    /// <summary>
    /// Reads initial biomass and the default harvest control rule reference levels, and calculates fishing mortality for each species as initial catch divided by initial biomass.
    /// </summary>
    /// <param name="scenarioName">The scenario subdirectory containing the initial HCR data file.</param>
    /// <param name="cancellationToken">A token that can cancel the read.</param>
    /// <returns>Initial HCR data keyed by the complete species identity. Reference levels that are absent from the file are returned as zero.</returns>
    /// <exception cref="InvalidDataException">Catch is missing or invalid, or a species has zero biomass.</exception>
    Task<Dictionary<SpeciesKey, InitialHcrData>> ReadInitialHCRDataAsync(string scenarioName, CancellationToken cancellationToken);
}
