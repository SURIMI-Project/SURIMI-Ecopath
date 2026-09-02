using EwECore;
using EwECore.Auxiliary;

namespace Ecopath.EwE.Wrapper
{
    /// <summary>
    /// this interface defines the cCore functionality that is used by the Ecopath application.
    /// </summary>
    public interface IEwECore
    {
        IPluginManager PluginManager { get; }
        cEcopathDataStructures EcopathDataStructures { get; }
        cEcospaceDataStructures EcospaceDataStructures { get; }
        cEcospaceBasemap EcospaceBasemap { get; }
        int nFleets { get; }
        bool EcospacePaused { get; set; }
        bool LoadModel(string modelName);
        bool RunEcopath(ref bool isBalanced);
        bool LoadEcosimScenario(int scenario);
        bool LoadTimeSeries(int timeSeries);
        cEcoSimModelParameters EcosimModelParameters { get; }
        bool RunEcosim();
        bool LoadEcospaceScenario(int scenario);
        void DiscardChanges();
        void CloseModel();
        void StopEcospace();
        void RunEcospace(ref cCore.EcoSpaceInterfaceDelegate? dgt);
        void Dispose();
        cEcospaceModelParameters EcospaceModelParameters { get; }
        public cAuxiliaryData AuxillaryData(string strValueId);
        cEcoPathGroupInput get_EcopathGroupInputs(int iGroup);
        cTaxon get_Taxon(int iTaxa);
        cStanzaGroup get_StanzaGroups(int v);
        cCoreInputOutputBase? get_EcopathFleetInputs(int index);


        /// <summary>
        /// Get/set the output path for simulation results.     
        /// </summary>
        string OutputPath { get; set; }

        public int nGroups { get; }
        int nTaxon { get; }

        // --- Lifecycle ---

        /// <summary>Creates the underlying cCore and cPluginManager. Must be called before any other member.</summary>
        void Initialize();

        /// <summary>Closes and disposes the underlying cCore and cPluginManager, resetting this instance to an uninitialized state.</summary>
        void Teardown();

        // --- Utility methods ---

        DateTime EcosimTimestepToAbsoluteTime(int iTimestep);
        int AbsoluteTimeToEcosimTimestep(DateTime dt);

        DateTime EcospaceTimestepToAbsoluteTime(int iTimestep);
        int AbsoluteTimeToEcospaceTimestep(DateTime dt);
    }
}
