using EwECore;
using EwEPlugin;

namespace EwECore
{
    public interface ICore : IDisposable
    {
        cPluginManager PluginManager { get; set; }
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
        DateTime EcospaceTimestepToAbsoluteTime(int iTime);
    }
}


/*
 * 
 * VB.Net
Imports EwECore
Imports EwEPlugin

Public Interface ICore
    Inherits IDisposable

    Property PluginManager As cPluginManager
    ReadOnly Property EcopathDataStructures As cEcopathDataStructures
    ReadOnly Property EcospaceDataStructures As cEcospaceDataStructures
    ReadOnly Property EcospaceBasemap As cEcospaceBasemap
    ReadOnly Property nFleets As Integer
    Property EcospacePaused As Boolean

    Function LoadModel(modelName As String) As Boolean
    Function RunEcopath(ByRef isBalanced As Boolean) As Boolean
    Function LoadEcosimScenario(scenario As Integer) As Boolean
    Function LoadTimeSeries(timeSeries As Integer) As Boolean
    ReadOnly Property EcosimModelParameters As cEcoSimModelParameters
    Function RunEcosim() As Boolean
    Function LoadEcospaceScenario(scenario As Integer) As Boolean
    Sub DiscardChanges()
    Sub CloseModel()
    Sub StopEcospace()
    Sub RunEcospace(ByRef dgt As cCore.EcoSpaceInterfaceDelegate)
    Function EcospaceTimestepToAbsoluteTime(iTime As Integer) As DateTime
End Interface * 
 */
