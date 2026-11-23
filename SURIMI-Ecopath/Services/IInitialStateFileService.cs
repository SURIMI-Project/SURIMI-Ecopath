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
    /// Reads initial biomass and calculates fishing mortality for each species as initial catch divided by initial biomass.
    /// </summary>
    /// <param name="scenarioName">The scenario subdirectory containing the initial HCR data file.</param>
    /// <param name="cancellationToken">A token that can cancel the read.</param>
    /// <returns>Biomass and fishing mortality values keyed by the complete species identity.</returns>
    /// <exception cref="InvalidDataException">Catch is missing or invalid, or a species has zero biomass.</exception>
    Task<Dictionary<SpeciesKey, (double Biomass, double FishingMortality)>> ReadInitialHCRDataAsync(string scenarioName, CancellationToken cancellationToken);

    /// <summary>
    /// Calculates initial fishing mortality for each species as initial catch divided by initial biomass.
    /// </summary>
    /// <param name="scenarioName">The scenario subdirectory containing the initial HCR data file.</param>
    /// <param name="cancellationToken">A token that can cancel the read.</param>
    /// <returns>Fishing mortality values keyed by the complete species identity.</returns>
    /// <exception cref="InvalidDataException">Catch is missing or invalid, or a species has zero biomass.</exception>
    Task<Dictionary<SpeciesKey, double>> ReadFishingMortalitiesAsync(string scenarioName, CancellationToken cancellationToken);
}
