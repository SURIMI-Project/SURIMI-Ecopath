using SURIMI.Datamodel;

namespace Ecopath.Services;

/// <summary>
/// Collects initialization-time catch data and optionally writes HCR initialization snapshots.
/// </summary>
public interface IHcrInitialisationFileService
{
    /// <summary>
    /// Gets whether HCR initialization files are enabled for the current simulation.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Starts a new simulation's collection, reading the <c>CREATE_HCR_INITIALISE_FILES</c> setting and clearing prior catch history.
    /// </summary>
    void BeginInitialization();

    /// <summary>
    /// Records a spin-up timestep's catch summary for the rolling year. Gross catch includes dead discards.
    /// Only call while EwE reports that spin-up is active; regular simulation catches must not enter this history.
    /// </summary>
    /// <param name="catchSummary">Catch summary produced for the completed spin-up timestep.</param>
    void CaptureCatchTimestep(CatchDispositionSummary catchSummary);

    /// <summary>
    /// Writes stock-recruitment parameters to the scenario output directory without requiring catch history.
    /// </summary>
    Task WriteRecruitmentFileAsync(IReadOnlyCollection<MseRecruitment> recruitment, string scenarioName, CancellationToken cancellationToken);

    /// <summary>
    /// Writes fleet quota shares to the scenario output directory without requiring catch history.
    /// </summary>
    Task WriteQuotaShareFileAsync(IReadOnlyCollection<MseQuotaShare> quotaShares, string scenarioName, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the initial biomass snapshot and the catch totals from the final 12 collected spin-up timesteps to <c>InitialHCRData.csv</c>.
    /// </summary>
    /// <param name="biomass">Biomass snapshot taken at the initial simulation pause.</param>
    /// <param name="scenarioName">Scenario whose output directory will contain the file.</param>
    /// <param name="cancellationToken">Token used to cancel the blob-store upload.</param>
    /// <returns>A task that completes when the CSV file has been written.</returns>
    /// <exception cref="InvalidOperationException">Files are disabled, the initial biomass snapshot is unavailable, or fewer than 12 catch timesteps have been collected.</exception>
    Task WriteInitialFilesAsync(Biomass biomass, string scenarioName, CancellationToken cancellationToken);
}

/// <summary>Stock-recruitment parameters for a single species and life stage.</summary>
public sealed record MseRecruitment(string SpeciesCode, string LifeStage, float RstockRatio, float RHalfB0Ratio, float CvRec);

/// <summary>Quota shares for a species and life stage, keyed by mapped gear and country.</summary>
public sealed record MseQuotaShare(string SpeciesCode, string LifeStage, IReadOnlyDictionary<(string GearCode, string CountryCode), double> Shares);
