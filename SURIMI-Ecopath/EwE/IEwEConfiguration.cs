using Ecopath.EwE.Wrapper;
using Ecopath.Generic;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Utils;
using SURIMI.Datamodel;

namespace Ecopath.EwE
{
    public interface IEwEConfiguration : IMEMConfiguration
    {
        int EcosimScenario { get; set; }
        int EcosimTimeSeries { get; set; }
        int MaxRunYears { get; set; }
        int EcospaceScenario { get; set; }
        int[] FishedGroups { get; }
        int SpinupYears { get; set; }
        bool IncludeVocabularies { get; set; }
        int StartYear { get; set; }

        IEnumerable<EwEMapping> Mappings(KeyDomain domain);
        bool Load(IEwECore core, SurimiConfiguration surimiConfiguration);
        IEnumerable<EwEMappingMatch> ResolveMarkets(string gearcode, string marketcode);
        IEnumerable<EwEMappingMatch> ResolveGroups(string speciescode);
        IEnumerable<EwEMappingMatch> ResolveGroups(MultiLevelKey key);
        IEnumerable<EwEMappingMatch> ResolveGroups(SURIMI.Datamodel.Species species);
        IEnumerable<EwEMappingMatch> ResolveFleets(SURIMI.Datamodel.FleetSegment fleetsegment);
        EwEMapping? Find(int iIndex, KeyDomain domain);
        bool IsExternalFleet(int iFleet);
    }
}
