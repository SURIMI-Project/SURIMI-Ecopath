using EwECore;
using EwECore.Auxiliary;
using EwECore.Plugins;

namespace Ecopath.EwE.Wrapper
{
    /// <summary>
    /// This class encapsulates the cCore, and provides an implementation of the <see cref="IEwECore"/> interface.
    /// <para>
    /// Instances start uninitialized. Call <see cref="Initialize"/> before using any other member,
    /// and <see cref="Teardown"/> to release all resources afterwards.
    /// </para>
    /// </summary>
    public class EwECore : IEwECore
    {
        /// <summary>The <see cref="cCore"/> to operate on. Null until <see cref="Initialize"/> is called.</summary>
        private cCore? m_core;
        private IPluginManager? m_pluginManager;

        public EwECore()
        {
            // Intentionally empty — cCore and cPluginManager are created lazily in Initialize().
        }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public void Initialize()
        {
            m_core = new cCore();
            m_core.PluginManager = new cPluginManager();
            m_pluginManager = new PluginManager(m_core.PluginManager);
        }

        /// <inheritdoc/>
        public void Teardown()
        {
            if (m_core == null) return;
            m_core.CloseModel();
            m_core.Dispose();
            m_core = null;
            m_pluginManager = null;
        }

        // -----------------------------------------------------------------------
        // Guard
        // -----------------------------------------------------------------------

        private cCore Core
        {
            get
            {
                if (m_core == null)
                    throw new InvalidOperationException("EwECore is not initialized. Call Initialize() first.");
                return m_core;
            }
        }

        // -----------------------------------------------------------------------
        // IEwECore members
        // -----------------------------------------------------------------------

        public cEcopathDataStructures EcopathDataStructures => Core.EcopathDataStructures;

        public cEcospaceDataStructures EcospaceDataStructures => Core.EcospaceDataStructures;

        public cEcospaceBasemap EcospaceBasemap => Core.EcospaceBasemap;

        public int nFleets => Core.nFleets;

        public bool EcospacePaused { get => Core.EcospacePaused; set => Core.EcospacePaused = value; }

        public cEcoSimModelParameters EcosimModelParameters => Core.EcosimModelParameters;

        public cEcospaceModelParameters EcospaceModelParameters => Core.EcospaceModelParameters;

        public void CloseModel() => Core.CloseModel();

        public void DiscardChanges() => Core.DiscardChanges();

        public void Dispose() => Core.Dispose();

        public bool LoadEcosimScenario(int scenario) => Core.LoadEcosimScenario(scenario);

        public bool LoadEcospaceScenario(int scenario) => Core.LoadEcospaceScenario(scenario);

        public bool LoadModel(string modelName) => Core.LoadModel(modelName);

        public bool LoadTimeSeries(int timeSeries) => Core.LoadTimeSeries(timeSeries);

        public bool RunEcopath(ref bool isBalanced) => Core.RunEcopath(ref isBalanced);

        public bool RunEcosim() => Core.RunEcosim();

        public void RunEcospace(ref cCore.EcoSpaceInterfaceDelegate? dgt) => Core.RunEcospace(ref dgt);

        public void StopEcospace() => Core.StopEcospace();

        public cAuxiliaryData AuxillaryData(string strValueId) => Core.get_AuxillaryData(strValueId);

        public cEcoPathGroupInput get_EcopathGroupInputs(int iGroup) => Core.get_EcopathGroupInputs(iGroup);

        public cTaxon get_Taxon(int iTaxa) => Core.get_Taxon(iTaxa);

        public cStanzaGroup get_StanzaGroups(int v) => Core.get_StanzaGroups(v);

        public cCoreInputOutputBase? get_EcopathFleetInputs(int index) => Core.get_EcopathFleetInputs(index);

        public int nGroups => Core.nGroups;

        public int nTaxon => Core.nTaxon;

        IPluginManager IEwECore.PluginManager
        {
            get
            {
                if (m_pluginManager == null)
                    throw new InvalidOperationException("EwECore is not initialized. Call Initialize() first.");
                return m_pluginManager;
            }
        }

        public string OutputPath { get => Core.OutputPath; set => Core.OutputPath = value; }

        public DateTime EcosimTimestepToAbsoluteTime(int iTime) => Core.EcosimTimestepToAbsoluteTime(iTime);
        public int AbsoluteTimeToEcosimTimestep(DateTime dt) => Core.AbsoluteTimeToEcosimTimestep(dt);

        public DateTime EcospaceTimestepToAbsoluteTime(int iTime) => Core.EcospaceTimestepToAbsoluteTime(iTime);
        public int AbsoluteTimeToEcospaceTimestep(DateTime dt) => Core.AbsoluteTimeToEcospaceTimestep(dt);
    }
}
