using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Core;
using SURIMI.Datamodel;

namespace Ecopath.Services
{
    public interface IEwEConfigurationService 
    {
        Task<IEwEConfiguration> CreateConfigurationAsync(string scenarioName);
        Task<bool> LoadAsync(IEwECore core, IEwEConfiguration configuration, SurimiContract surimiContract);
        IEnumerable<EwEMappingMatch> ResolveMarkets(string gearcode, string marketcode);
        IEnumerable<EwEMappingMatch> ResolveGroups(string speciescode);
        IEnumerable<EwEMappingMatch> ResolveGroups(MultiLevelKey key);
        IEnumerable<EwEMappingMatch> ResolveGroups(SURIMI.Datamodel.Species species);
        IEnumerable<EwEMappingMatch> ResolveFleets(SURIMI.Datamodel.FleetSegment fleetsegment);

        /// <summary>
        /// Finds the mapping for a given index (in cCore, 1 based) and domain (KeyDomain.Species, KeyDomain.FleetSegment, KeyDomain.Market, etc.).
        /// </summary>
        /// <param name="iIndex">The index is the (1 BASED!!!!!!) index that the fleet or group has in cCore. 
        /// You can look it up in the EwE application. 
        /// Input > Basic input holds the species. 
        /// Input > Fishery > Fleets holds the fleets.</param>
        /// <param name="domain">The domain of the key (e.g., KeyDomain.FleetSegment, KeyDomain.Species, KeyDomain.Market, etc.).</param>
        /// <returns>The mapping for the given index and domain, or null if not found.</returns>
        EwEMapping? FindMapping(int iIndex, KeyDomain domain);

        /// <summary>
        /// Get all item mappings for a specific domain
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        IEnumerable<EwEMapping> Mappings(KeyDomain domain);
    }
}
