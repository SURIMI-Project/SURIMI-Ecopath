using SURIMI.Datamodel;

namespace Ecopath.EwE
{
    public interface IEwEController
    {
        EwEController.RunStates RunState { get; }
        bool IsWaiting { get; }
        Task<int> StartAsync(SurimiContract surimiContract, string scenarioName, int timeoutMs = 60000);
        Task<bool> ContinueAsync(int timeoutMs = 60000);
        Task<bool> StopAsync(int timeoutMs = 10000);
        Task<bool> UpdatePricesAsync(List<SpeciesPrice> speciesPrices);
        Task<Biomass> GetBiomassAsync();
        Task<List<SalesSummary>> GetSalesSummariesAsync(DateTime start, DateTime end);
        Task<CatchDispositionSummary> GetCatchDispositionSummaryAsync(DateTime start, DateTime end);
        Task<bool> UpdateCatchDispositionSummaryAsync(CatchDispositionSummary catchDispositionSummary);
        Task<bool> UpdateEnvironmentVariablesAsync(EnvironmentVariablesSummary environmentVariables);
        Task<bool> UpdateRegulationsAsync(RegulationsSummary regulations);
        Task<FishingActivitySummary> GetFishingActivityAsync();
    }
}
