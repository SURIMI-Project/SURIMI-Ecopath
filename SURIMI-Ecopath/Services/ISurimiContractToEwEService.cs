using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using SURIMI.Datamodel;

namespace Ecopath.Services
{
    public interface ISurimiContractToEwEService
    {
        Task AddSurimiContractFleetToMappingsAsync(List<EwEMapping> mappings, IEwECore core, SurimiContract surimiContract);
    }
}
