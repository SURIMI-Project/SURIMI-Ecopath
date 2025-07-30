using Ecopath.Generic;
using EwECore;

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
        bool Load(cCore core);
        IEnumerable<EwEMappingMatch> ResolveMarkets(string gearcode, string marketcode);
        IEnumerable<EwEMappingMatch> ResolveGroups(string speciescode);
        IEnumerable<EwEMappingMatch> ResolveGroups(MultiLevelKey key);
        IEnumerable<EwEMappingMatch> ResolveGroups(Models.Species species);
        IEnumerable<EwEMappingMatch> ResolveFleets(Models.FleetSegment fleetsegment);
        EwEMapping? Find(int iIndex, KeyDomain domain);
        bool IsExternalFleet(int iFleet);
    }
}
