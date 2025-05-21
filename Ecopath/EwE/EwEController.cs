using Ecopath.Models;
using EwECore;
using EwEPlugin;
using EwEUtils.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using static EwECore.cCore;

namespace Ecopath.EwE
{
    public class EwEController : IEwEController
    {
        #region Private vars 
        
        /// <summary>The <see cref="cCore"/> to operate on.</summary>
        private readonly cCore _core;
        /// <summary>The Ecospace run thread, if any.</summary>
        private Thread? _thread;
        /// <summary>Core message handler for tracking EwE execution flow.</summary>
        private cMessageHandler? _mh;

        private readonly ILogger<EwEController> _logger;
        private EwEConfiguration? _configuration;

        private RunStates _runstate = RunStates.idle;

        /// <summary>Event for internal state monitoring.</summary>
        private event Action<RunStates>? OnRunStateChanged;

        // --- Cached run info
        private List<SpeciesPrice>? _prices;
        private Biomass? _biomass;

        #endregion // Private vars 

        /// <summary>
        /// Enumerated type, defining the possible run states of the EwEController.
        /// </summary>
        public enum RunStates : uint
        {
            /// <summary>Ready to be started.</summary>
            idle = 0,
            /// <summary>Starting up, not ready yet.</summary>
            starting,
            /// <summary>Waiting for exteral input.</summary>
            waiting,
            /// <summary>Busy running simulations.</summary>
            running,
            /// <summary>Busy stoppping.</summary>
            stopping
        }

        public EwEController(ILogger<EwEController> logger)
        {

            _core = new cCore();
            cLog.VerboseLevel = eVerboseLevel.Disabled; // Turn off all internal event logging
            RunState = RunStates.idle;

            _mh = new cMessageHandler(OnCoreMessage, eCoreComponentType.Ecospace, eMessageType.EcospaceRunCompleted, SynchronizationContext.Current);
            _core.Messages.AddMessageHandler(_mh);

            // To make sure we can find local resources. This is rather hack.
            Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
            _logger = logger;
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
        public async Task<int> StartAsync(EwEConfiguration config, int timeoutMs = 60000)
        {
            if (RunState != RunStates.idle)
            {
                throw new Exception("EwE controller already busy, aborting");
            }

            RunState = RunStates.starting;
            _configuration = config;

            _core.PluginManager = new cPluginManager();
            _logger.LogInformation("EwE loaded {0} plug-in(s)", _core.PluginManager.LoadPlugins());

            if (!File.Exists(_configuration.ModelName))
            {
                throw new FileNotFoundException("EwE model file '{0}' cannot be found", _configuration.ModelName); 
            }

            if (!_core.LoadModel(_configuration.ModelName))
            {
                throw new Exception($"EwE could not load model '{_configuration.ModelName}'");
            }
            _logger.LogInformation("EwE - Ecopath loaded model '{0}'", _configuration.ModelName);

            bool bIsBalanced = false;
            if (!_core.RunEcopath(ref bIsBalanced) | !bIsBalanced)
            {
                throw new Exception("EwE - Ecopath does not balance");
            }
            _logger.LogInformation("EwE - Ecopath does balance"); 

            if (_configuration.EcosimScenario <= 0 | !_core.LoadEcosimScenario(_configuration.EcosimScenario))
            {
                throw new Exception($"EwE - Ecosim scenario {_configuration.EcosimScenario} not loaded");
            }
            _logger.LogInformation("EwE - Ecosim scenario {0} loaded", _configuration.EcosimScenario);

            if (_configuration.EcosimTimeSeries > 0)
            {
                if (!_core.LoadTimeSeries(_configuration.EcosimTimeSeries))
                {
                    throw new Exception($"EwE - Ecosim time series {_configuration.EcosimTimeSeries} not loaded");
                }
                _logger.LogInformation("EwE - Ecosim time series {0} loaded", _configuration.EcosimTimeSeries);
            }

            cEcoSimModelParameters parms = _core.EcosimModelParameters;
            parms.NumberYears = _configuration.MaxRunYears; // No of years apply to both Sim and Space

            if (!_core.RunEcosim())
            {
                throw new Exception("EwE - Ecosim failed to run");
            }
            _logger.LogInformation("EwE - Ecosim run successfully");

            if (_configuration.EcospaceScenario <= 0 | !_core.LoadEcospaceScenario(_configuration.EcospaceScenario))
            {
                throw new Exception($"EwE - Ecospace scenario {_configuration.EcospaceScenario} not loaded");
            }
            _logger.LogInformation("EwE - Ecospace scenario {0} loaded", _configuration.EcospaceScenario);

            cEcospaceDataStructures ds = _core.EcospaceDataStructures;
            ds.SpinUpYears = _configuration.SpinupYears;
            ds.UseSpinUp = (_configuration.SpinupYears > 0);
            _logger.LogInformation("EwE - Ecospace spin-up {0}", ds.UseSpinUp ? _configuration.SpinupYears.ToString() : "off"); 

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
        public async Task<bool> ContinueAsync(int timeoutMs = 60000)
        {
            if (RunState != RunStates.waiting) return false;
    
            // Need to wait for RunState to switch back to Waiting. Only return after
            var tcs = new TaskCompletionSource();

            void Handler(RunStates state)
            {
                if (state == RunStates.waiting)
                {
                    tcs.TrySetResult();
                }
            }
            OnRunStateChanged += Handler;

            // Carry on
            _core.EcospacePaused = false;
            RunState = RunStates.running;

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            OnRunStateChanged -= Handler;

            _logger.LogInformation("EwE - continue");
            return true;
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
            if (RunState == RunStates.stopping) return;

            // Do not halt while in spinup
            cEcospaceDataStructures ds = _core.EcospaceDataStructures;
            if (ds.bInSpinUp) return;
            if (timestep.TimeStepinYears < _configuration?.StartYear) return; 
            //if (_core.EcosimFirstYear() + timestep.TimeStepinYears < _configuration?.StartYear) return; // Should use absolute start year instead; is more robust

            _logger.LogInformation(string.Format("EwE - pausing at timestep {0}, {1}", timestep.iTimeStep, _core.EcospaceTimestepToAbsoluteTime(timestep.iTimeStep)));

            RunState = RunStates.waiting;
            _core.EcospacePaused = true;

            try
            {
                // Build relevant biomass grids for the first tmie 
                BuildBiomassCache(timestep);
            }
            catch (Exception ex)
            {
                //_logger.LogInformation("EwE - exception ...");
            }
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
                //_logger.LogInformation("EwE - exception ...");
            }

            RunState = RunStates.idle; // Manually reset to idle if needed
        }

        public Task<bool> UpdatePricesAsync(List<SpeciesPrice> speciesPrices)
        {
            _prices = speciesPrices;
            return Task.FromResult(true);
        }

        public Task<Biomass> GetBiomassAsync()
        {
            if (_biomass == null)
                _biomass = new Biomass();
            return Task.FromResult(_biomass);
        }

        void Clear()
        {
            _prices = null;
            _biomass = null;
        }

        void BuildBiomassCache(cEcospaceTimestep timestep)
        {
            _biomass = new Biomass()
            {
                MeasurementUnit = "kg"
            };
            if (_configuration != null)
            {
                cEcospaceBasemap bm = _core.EcospaceBasemap;
                cEcospaceDataStructures ds = _core.EcospaceDataStructures;

                foreach (string spp in _configuration.SpeciesOfInterest())
                {
                    BiomassGrid grid = new BiomassGrid()
                    {
                        SpeciesId = spp
                    };
                    int iGroup = _configuration.get_SpeciesGroup(spp);
                    Single scalar = _configuration.get_SpeciesContribution(spp);

                    for (int ic = 1; ic <= ds.InCol; ic++)
                        for (int ir = 1; ir <= ds.InRow; ir++)  
                            if (bm.IsModelledCell(ir, ic))
                            {
                                grid.BiomassCells.Add(new BiomassCell()
                                {
                                    Latitude = bm.RowToLat(ir),
                                    Longitude = bm.ColToLon(ic),
                                    Biomass = ds.Bcell[ir, ic, iGroup] * scalar
                                });
                            }
                    _biomass.BiomassGrids.Add(grid);
                }
            }
        }
        #endregion // Internals
    }
}
