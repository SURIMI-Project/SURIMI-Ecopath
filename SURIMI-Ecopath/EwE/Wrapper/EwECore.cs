using EwECore;
using EwECore.Auxiliary;
using EwEPlugin;

namespace Ecopath.EwE.Wrapper
{
    /// <summary>
    /// This class encapsulates the cCore, and provides an implementation of the <see cref="IEwECore"/> interface,
    /// </summary>
    public class EwECore : IEwECore
    {
        /// <summary>The <see cref="cCore"/> to operate on.</summary>
        private readonly cCore m_core;
        private readonly IPluginManager m_pluginManager;

        public EwECore()
        {
            m_core = new cCore();
            m_core.PluginManager = new cPluginManager();
            m_pluginManager = new PluginManager(m_core.PluginManager);
        }

        public cEcopathDataStructures EcopathDataStructures => m_core.EcopathDataStructures;

        public cEcospaceDataStructures EcospaceDataStructures => m_core.EcospaceDataStructures;

        public cEcospaceBasemap EcospaceBasemap => m_core.EcospaceBasemap;

        public int nFleets => m_core.nFleets;

        public bool EcospacePaused { get => m_core.EcospacePaused; set => m_core.EcospacePaused = value; }

        public cEcoSimModelParameters EcosimModelParameters => m_core.EcosimModelParameters;

        public cEcospaceModelParameters EcospaceModelParameters => m_core.EcospaceModelParameters;

        public void CloseModel() => m_core.CloseModel();

        public void DiscardChanges() => m_core.DiscardChanges();

        public void Dispose() => m_core.Dispose();

        public DateTime EcospaceTimestepToAbsoluteTime(int iTime) => m_core.EcospaceTimestepToAbsoluteTime(iTime);

        public bool LoadEcosimScenario(int scenario) => m_core.LoadEcosimScenario(scenario);

        public bool LoadEcospaceScenario(int scenario) => m_core.LoadEcospaceScenario(scenario);

        public bool LoadModel(string modelName) => m_core.LoadModel(modelName);

        public bool LoadTimeSeries(int timeSeries) => m_core.LoadTimeSeries(timeSeries);

        public bool RunEcopath(ref bool isBalanced) => m_core.RunEcopath(ref isBalanced);

        public bool RunEcosim() => m_core.RunEcosim();

        public void RunEcospace(ref cCore.EcoSpaceInterfaceDelegate? dgt) => m_core.RunEcospace(ref dgt);

        public void StopEcospace() => m_core.StopEcospace();

        public cAuxiliaryData AuxillaryData(string strValueId) => m_core.get_AuxillaryData(strValueId);

        public cEcoPathGroupInput get_EcopathGroupInputs(int iGroup) => m_core.get_EcopathGroupInputs(iGroup);

        public cTaxon get_Taxon(int iTaxa) => m_core.get_Taxon(iTaxa);

        public cStanzaGroup get_StanzaGroups(int v) => m_core.get_StanzaGroups(v);

        public cCoreInputOutputBase? get_EcopathFleetInputs(int index) => m_core.get_EcopathFleetInputs(index);

        public int nGroups => m_core.nGroups;

        public int nTaxon => m_core.nTaxon;

        IPluginManager IEwECore.PluginManager { get => m_pluginManager; }
    }
}
