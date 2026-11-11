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
    /// Reads species biomass values from the scenario's <c>initial_biomasses.csv</c> input file.
    /// </summary>
    /// <param name="scenarioName">The scenario subdirectory containing the input file.</param>
    /// <param name="cancellationToken">A token that can cancel the read.</param>
    /// <returns>Biomass values keyed by the complete species identity.</returns>
    Task<Dictionary<SpeciesKey, double>> ReadBiomassesAsync(string scenarioName, CancellationToken cancellationToken);

    /// <summary>
    /// Calculates initial fishing mortality for each species as initial catch divided by initial biomass.
    /// </summary>
    /// <param name="scenarioName">The scenario subdirectory containing the initial biomass and catch files.</param>
    /// <param name="cancellationToken">A token that can cancel the reads.</param>
    /// <returns>Fishing mortality values keyed by the complete species identity.</returns>
    /// <exception cref="InvalidDataException">The files do not contain matching species, or a species has zero biomass.</exception>
    Task<Dictionary<SpeciesKey, double>> ReadFishingMortalitiesAsync(string scenarioName, CancellationToken cancellationToken);
}
