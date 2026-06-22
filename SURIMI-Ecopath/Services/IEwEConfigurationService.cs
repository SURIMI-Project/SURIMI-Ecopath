using Ecopath.EwE.Wrapper;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Core;
using SURIMI.Datamodel;

namespace Ecopath.EwE
{
    public interface IEwEConfigurationService 
    {
        Task<IEwEConfiguration> CreateConfigurationAsync(string scenarioName);
        bool Load(IEwECore core, IEwEConfiguration configuration, SurimiContract surimiContract);
        IEnumerable<EwEMappingMatch> ResolveMarkets(string gearcode, string marketcode);
        IEnumerable<EwEMappingMatch> ResolveGroups(string speciescode);
        IEnumerable<EwEMappingMatch> ResolveGroups(MultiLevelKey key);
        IEnumerable<EwEMappingMatch> ResolveGroups(SURIMI.Datamodel.Species species);
        IEnumerable<EwEMappingMatch> ResolveFleets(SURIMI.Datamodel.FleetSegment fleetsegment);
        EwEMapping? Find(int iIndex, KeyDomain domain);
        IEnumerable<EwEMapping> Mappings(KeyDomain domain);
    }
}
