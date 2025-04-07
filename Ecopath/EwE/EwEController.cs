using EwECore;
using EwEPlugin;
using EwEUtils.Core;
using System.Threading;
using static EwECore.cCore;

namespace Ecopath.EwE
{
    public class EwEController
    {
        #region Private vars 
        
        /// <summary>The <see cref="cCore"/> to operate on.</summary>
        private readonly cCore _core;
        /// <summary>The Ecospace run thread, if any.</summary>
        private Thread? _thread;
        /// <summary>Core message handler for tracking EwE execution flow.</summary>
        private cMessageHandler? _mh;

        private RunStates _runstate = RunStates.idle;

        /// <summary>Event for internal state monitoring.</summary>
        private event Action<RunStates>? OnRunStateChanged;

        #endregion // Private vars 

        public enum RunStates
        {
            idle, // Ready to be started
            starting, // Starting up, not ready yet
            waiting, // Waiting for exteral input
            running, // Busy running simulations
            stopping // Busy stoppping
        }

        public EwEController() {

            _core = new cCore();
            RunState = RunStates.idle;

            _mh = new cMessageHandler(OnCoreMessage, eCoreComponentType.Ecospace, eMessageType.EcospaceRunCompleted, SynchronizationContext.Current);
            _core.Messages.AddMessageHandler(_mh);

            // To make sure we can find local resources. THis is rather hack.
            Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
        }

        ~EwEController()
        {
            _core.Messages.RemoveMessageHandler(_mh);
            _mh = null;

            ForceStop();

            _core.CloseModel();
            _core.Dispose();
        }

        #region Public interaction 

        /// <summary>
        /// The configuration that EwE is running against
        /// </summary>
        public EwEConfiguration? Configuration { get; private set; }

        /// <summary>
        /// The current EwE run state.
        /// </summary>
        public RunStates RunState 
        { get => _runstate; 
            private set
            {
                if (_runstate != value)
                {
                    _runstate = value;
                    OnRunStateChanged?.Invoke(_runstate);
                }
            }
        } 

        /// <summary>
        /// Helper method, returns if Ecospace is waiting for input.
        /// </summary>
        public bool IsWaiting { get { return RunState == RunStates.waiting; } }

        /// <summary>
        /// Start EwE and wait for Ecospace to get ready for simulations
        /// </summary>
        /// <returns></returns>
        public async Task<int> StartAsync(int timeoutMs = 60000)
        {
            if (RunState != RunStates.idle)
            {
                Console.WriteLine("EwE controller already busy, aborting"); // ToDo: log this
                return -1; // ToDo: return informative error code?
            }

            RunState = RunStates.starting;

            // Todo: this needs to come from somewhere
            this.Configuration = new EwEConfiguration
            {
                ModelName = Path.Combine(Directory.GetCurrentDirectory(), @"Includes\Anchovy Bay Spatial.eiixml"),
                EcosimScenario = 1,
                EcosimTimeSeries = 0,
                EcospaceScenario = 1,
                SpinupYears = 10,
                StartYear = 5
            };

            _core.PluginManager = new cPluginManager();
            Console.WriteLine("EwE loaded {0} plug-in(s)", _core.PluginManager.LoadPlugins()); // ToDo: log this

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
            if (!_core.RunEcopath(ref bIsBalanced) | !bIsBalanced)
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

            var tcs = new TaskCompletionSource();

            void Handler(RunStates state)
            {
                if (state == RunStates.waiting)
                {
                    tcs.TrySetResult();
                }
            }
            OnRunStateChanged += Handler;

            // Phew, we managed to plow through. Run Ecospace!
            _thread = new Thread(RunEcospace);
            _thread.Start();

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            OnRunStateChanged -= Handler;

            return (RunState == RunStates.waiting) ? 1: -1;
        }

        /// <summary>
        /// We might as well make this an async method too, even though there won't be any waiting
        /// </summary>
        /// <returns></returns>
        public int Continue()
        {
            if (RunState != RunStates.waiting) return -1;

            // Carry on
            _core.EcospacePaused = false;
            RunState = RunStates.running;

            Console.WriteLine("EwE - continue");
            return 0;
        }

        /// <summary>
        /// Stop any simulation
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StopAsync(int timeoutMs = 10000)
        {
            var tcs = new TaskCompletionSource();

            void Handler(RunStates state)
            {
                if (state == RunStates.idle)
                {
                    tcs.TrySetResult();
                }
            }

            OnRunStateChanged += Handler;

            _core.StopEcospace(); // Initiate graceful shutdown

            if (this.RunState == RunStates.idle)
            {
                OnRunStateChanged -= Handler;
                return true;
            }

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

            if (completedTask == tcs.Task)
            {
                return true; // All good
            }

            // Timeout hit: force kill
            OnRunStateChanged -= Handler;
            ForceStop();
            return false;
        }

        #endregion // Public interaction

        #region Internals

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
            if (RunState == RunStates.stopping) return;

            Console.WriteLine("EwE - pausing");

            RunState = RunStates.waiting;
            _core.EcospacePaused = true;
        }

        private void OnCoreMessage(ref cMessage msg)
        {
            switch (msg.Type)
            {
                case eMessageType.EcospaceRunCompleted:

                    // Clear all modifications made by the process
                    _core.DiscardChanges();
                    // Correctly reset the state and clean up
                    RunState = RunStates.idle;
                    _thread = null;
                    break;
            }

        }

        private void ForceStop()
        {
            try
            {
                if (_thread != null && _thread.IsAlive)
                    _thread.Interrupt();
            }
            catch (Exception ex)
            {
            }

            RunState = RunStates.idle; // Manually reset to idle if needed
        }

        #endregion // Internals
    }
}
