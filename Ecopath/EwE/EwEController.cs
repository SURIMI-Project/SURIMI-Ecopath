using EwECore;
using EwEPlugin;
using EwEUtils.Core;
using static EwECore.cCore;

namespace Ecopath.EwE
{
    public class EwEController
    {
        private readonly cCore _core;
        private readonly EventWaitHandle _pausewait; // ToDo: replace with something more modern
        private Thread? _thread;
        private cMessageHandler? _mh;

        public enum RunState
        {
            Idle, // Ready to be started
            Starting, // Starting up, not ready yet
            Waiting, // Waiting for exteral input
            Running, // Busy running simulations
            Stopping // Busy stoppping
        }
        private RunState _runstate = RunState.Idle;

        public EwEController() {

            _core = new cCore();
            _pausewait = new EventWaitHandle(false, EventResetMode.AutoReset); 

            _mh = new cMessageHandler(OnCoreMessage, eCoreComponentType.Ecospace, eMessageType.EcospaceRunCompleted, SynchronizationContext.Current);
            _core.Messages.AddMessageHandler(_mh);

            // To make sure we can find local resources. THis is rather hack.
            Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
        }

        ~EwEController()
        {
            _core.Messages.RemoveMessageHandler(_mh);
            _mh = null;

            Stop();
            _core.CloseModel();
            _core.Dispose();
        }

        public EwEConfiguration? Configuration { get; private set; }
        public bool IsWaiting { get; private set; } = false;

        public int Start()
        {
            if (_runstate != RunState.Idle)
            {
                Console.WriteLine("EwE controller already busy, aborting"); // ToDo: log this
                return -1; // ToDo: return informative error code?
            }

            _runstate = RunState.Starting;

            // Todo: this needs to come from somewhere
            this.Configuration = new EwEConfiguration { 
                ModelName =  Path.Combine(Directory.GetCurrentDirectory(), @"Includes\Anchovy Bay Spatial.eiixml"), 
                EcosimScenario = 1, 
                EcosimTimeSeries = 0, 
                EcospaceScenario = 1, 
                SpinupYears = 10, 
                StartYear = 5};

            _core.PluginManager = new cPluginManager();
            Console.WriteLine("EwE loaded {0} plug-in(s)",_core.PluginManager.LoadPlugins()); // ToDo: log this

            if (!File.Exists(Configuration.ModelName))
            {
                Console.WriteLine("EwE model file '{0}' cannot be found", Configuration.ModelName); // ToDo: log this
                return -1; // ToDo: return informative error code?
            }

            if (!_core.LoadModel(Configuration.ModelName))
            {
                Console.WriteLine("EwE could not load model '{0}'", Configuration.ModelName); // ToDo: log this
                return -1; // ToDo: return informative error code?
            }
            Console.WriteLine("EwE - Ecopath loaded model '{0}'", Configuration.ModelName); // ToDo: log this

            bool bIsBalanced = false;
            if (!_core.RunEcopath(ref bIsBalanced) | !bIsBalanced )
            {
                Console.WriteLine("EwE - Ecopath does not balance"); // ToDo: log this
                return -1; // ToDo: return informative error code?
            }
            Console.WriteLine("EwE - Ecopath does balance"); // ToDo: log this

            if (Configuration.EcosimScenario <= 0 | !_core.LoadEcosimScenario(Configuration.EcosimScenario))
            {
                Console.WriteLine("EwE - Ecosim scenario {0} not loaded", Configuration.EcosimScenario); // ToDo: log this
                return -1; // ToDo: return informative error code?
            }
            Console.WriteLine("EwE - Ecosim scenario {0} loaded", Configuration.EcosimScenario); // ToDo: log this

            if (Configuration.EcosimTimeSeries > 0)
            {
                if (!_core.LoadTimeSeries(Configuration.EcosimTimeSeries))
                {
                    Console.WriteLine("EwE - Ecosim time series {0} not loaded", Configuration.EcosimTimeSeries); // ToDo: log this
                    return -1; // ToDo: return informative error code?
                }
                Console.WriteLine("EwE - Ecosim time series {0} loaded", Configuration.EcosimTimeSeries); // ToDo: log this
            }

            cEcoSimModelParameters parms = _core.EcosimModelParameters;
            parms.NumberYears = Configuration.MaxRunYears; // No of years apply to both Sim and Space

            if (!_core.RunEcosim())
            {
                Console.WriteLine("EwE - Ecosim failed to run"); // ToDo: log this
                return -1; // ToDo: return informative error code?
            }
            Console.WriteLine("EwE - Ecosim run successfully"); // ToDo: log this

            if (Configuration.EcospaceScenario <= 0 | !_core.LoadEcospaceScenario(Configuration.EcospaceScenario))
            {
                Console.WriteLine("EwE - Ecospace scenario {0} not loaded", Configuration.EcospaceScenario); // ToDo: log this
                return -1; // ToDo: return informative error code?
            }
            Console.WriteLine("EwE - Ecospace scenario {0} loaded", Configuration.EcospaceScenario); // ToDo: log this

            cEcospaceDataStructures ds = _core.EcospaceDataStructures;
            ds.SpinUpYears = Configuration.SpinupYears;
            ds.UseSpinUp = (Configuration.SpinupYears > 0);
            Console.WriteLine("EwE - Ecospace spin-up {0}", ds.UseSpinUp ? Configuration.SpinupYears.ToString() : "off"); // ToDo: log this

            // Phew, we managed to plow through. Run Ecospace!
            _thread = new Thread(RunEcospace);
            _thread.Start();
            _pausewait.WaitOne();

            return 1;
        }

        public int Continue()
        {
            if (_runstate != RunState.Waiting) return -1;

            // Carry on
            _core.EcospacePaused = false;
            Console.WriteLine("EwE - continue");
            return 0;
        }

        public int Stop()
        {
            if (_runstate != RunState.Waiting) return -1;
            Console.WriteLine("EwE - stopping");
            try
            {
                _runstate = RunState.Stopping;
                _core.StopEcospace();
                _pausewait.WaitOne();
            }
            catch (Exception ex)
            {
                // ToDo: log this
            }
            _runstate = RunState.Idle;
            _thread = null;
            return 1;
        }

        private void RunEcospace()
        {
            cCore.EcoSpaceInterfaceDelegate dgt = new EcoSpaceInterfaceDelegate(EcospaceCallBack);
            _core.RunEcospace(ref dgt);
        }

        private void EcospaceCallBack(ref cEcospaceTimestep timestep)
        {
            // Do not halt while in spinup
            cEcospaceDataStructures ds = _core.EcospaceDataStructures;
            if (ds.bInSpinUp) return;
            if (timestep.TimeStepinYears < Configuration.StartYear) return;

            Console.WriteLine("EwE - pausing");

            IsWaiting = true;
            _pausewait.Set();
            _core.EcospacePaused = (_runstate != RunState.Stopping);
            IsWaiting = false;
        }

        private void OnCoreMessage(ref cMessage msg)
        {
            switch (msg.Type)
            {
                case eMessageType.EcospaceRunCompleted:
                    _pausewait.Set();

                    // Clear all modifications made by the process
                    _core.DiscardChanges();
                    _runstate = RunState.Idle;
                    break;
            }

        }
    }
}
