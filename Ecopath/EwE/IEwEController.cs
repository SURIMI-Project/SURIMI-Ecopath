using Ecopath.Models;
using System.Threading.Tasks;

namespace Ecopath.EwE
{
    public interface IEwEController
    {
        EwEController.RunStates RunState { get; }
        bool IsWaiting { get; }
        Task<int> StartAsync(int timeoutMs = 60000);
        int Continue();
        Task<bool> StopAsync(int timeoutMs = 10000);
        Task<bool> UpdatePricesAsync(List<SpeciesPrice> speciesPrices);
        Task<Biomass> GetBiomassAsync();
    }
}
