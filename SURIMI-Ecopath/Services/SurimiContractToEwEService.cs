using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Eii.SemanticRegistry;
using EwECore;
using SURIMI.Datamodel;

namespace Ecopath.Services
{
    public class SurimiContractToEwEService
    {
        private readonly ILogger<SurimiContractToEwEService> _logger;
        private readonly ISemanticRegistry _semanticRegistry;
        public SurimiContractToEwEService(ILogger<SurimiContractToEwEService> logger, ISemanticRegistry semanticRegistry)
        {
            _logger = logger;
            _semanticRegistry = semanticRegistry;
        }

        public void ConvertSurimiContractToEwE(List<EwEMapping> mappings, IEwECore core, SurimiContract surimiContract)
        {
            cEcopathDataStructures ecopathds = core.EcopathDataStructures;
            var fleetNames = ecopathds.FleetName;


        }
    }
}
