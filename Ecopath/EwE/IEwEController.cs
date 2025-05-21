using Ecopath.Models;

namespace Ecopath.EwE
{
    public interface IEwEController
    {
        EwEController.RunStates RunState { get; }
        bool IsWaiting { get; }
        Task<int> StartAsync(EwEConfiguration config, int timeoutMs = 60000);
        Task<bool> ContinueAsync(int timeoutMs = 60000);
        Task<bool> StopAsync(int timeoutMs = 10000);
        Task<bool> UpdatePricesAsync(List<SpeciesPrice> speciesPrices);
        Task<Biomass> GetBiomassAsync();
    }
}
