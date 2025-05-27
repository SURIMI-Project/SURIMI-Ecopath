using Ecopath.Models;
using EwECore;
using EwEPlugin;
using EwEUtils.Core;
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

        private RunStates _runstate = RunStates.idle;

        /// <summary>Event for internal state monitoring.</summary>
        private event Action<RunStates>? OnRunStateChanged;

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
        /// The configuration that EwE is running against
        /// </summary>
        public EwEConfiguration? Configuration { get; private set; }

        /// <summary>
        /// The current EwE run state.
        /// </summary>
        public RunStates RunState
        {
            get => _runstate;
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
                throw new Exception("EwE controller already busy, aborting");
            }

            RunState = RunStates.starting;

            // Todo: this needs to come from somewhere
            this.Configuration = new EwEConfiguration
            {
                ModelName = @"Includes/Anchovy Bay Spatial.eiixml",
                EcosimScenario = 1,
                EcosimTimeSeries = 0,
                EcospaceScenario = 1,
                SpinupYears = 10,
                StartYear = 5
            };

            _core.PluginManager = new cPluginManager();
            _logger.LogInformation("EwE loaded {0} plug-in(s)", _core.PluginManager.LoadPlugins());

            if (!File.Exists(Configuration.ModelName))
            {
                throw new FileNotFoundException("EwE model file '{0}' cannot be found", Configuration.ModelName);
            }

            if (!_core.LoadModel(Configuration.ModelName))
            {
                throw new Exception($"EwE could not load model '{Configuration.ModelName}'");
            }
            _logger.LogInformation("EwE - Ecopath loaded model '{0}'", Configuration.ModelName);

            bool bIsBalanced = false;
            if (!_core.RunEcopath(ref bIsBalanced) | !bIsBalanced)
            {
                throw new Exception("EwE - Ecopath does not balance");
            }
            _logger.LogInformation("EwE - Ecopath does balance");

            if (Configuration.EcosimScenario <= 0 | !_core.LoadEcosimScenario(Configuration.EcosimScenario))
            {
                throw new Exception($"EwE - Ecosim scenario {Configuration.EcosimScenario} not loaded");
            }
            _logger.LogInformation("EwE - Ecosim scenario {0} loaded", Configuration.EcosimScenario);

            if (Configuration.EcosimTimeSeries > 0)
            {
                if (!_core.LoadTimeSeries(Configuration.EcosimTimeSeries))
                {
                    throw new Exception($"EwE - Ecosim time series {Configuration.EcosimTimeSeries} not loaded");
                }
                _logger.LogInformation("EwE - Ecosim time series {0} loaded", Configuration.EcosimTimeSeries);
            }

            cEcoSimModelParameters parms = _core.EcosimModelParameters;
            parms.NumberYears = Configuration.MaxRunYears; // No of years apply to both Sim and Space

            if (!_core.RunEcosim())
            {
                throw new Exception("EwE - Ecosim failed to run");
            }
            _logger.LogInformation("EwE - Ecosim run successfully");

            if (Configuration.EcospaceScenario <= 0 | !_core.LoadEcospaceScenario(Configuration.EcospaceScenario))
            {
                throw new Exception($"EwE - Ecospace scenario {Configuration.EcospaceScenario} not loaded");
            }
            _logger.LogInformation("EwE - Ecospace scenario {0} loaded", Configuration.EcospaceScenario);

            cEcospaceDataStructures ds = _core.EcospaceDataStructures;
            ds.SpinUpYears = Configuration.SpinupYears;
            ds.UseSpinUp = (Configuration.SpinupYears > 0);
            _logger.LogInformation("EwE - Ecospace spin-up {0}", ds.UseSpinUp ? Configuration.SpinupYears.ToString() : "off");

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

            return (RunState == RunStates.waiting) ? 1 : -1;
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

            // Need to wait for RunState to switch back to Waiting. Only return after

            _logger.LogInformation("EwE - continue");
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
            if (timestep.TimeStepinYears < Configuration?.StartYear) return;
            //if (_core.EcosimFirstYear() + timestep.TimeStepinYears < Configuration?.StartYear) return; // Should use absolute start year instead; is more robust
            if (RunState == RunStates.stopping) return;

            _logger.LogInformation("EwE - pausing");

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

        public Task<bool> UpdatePricesAsync(List<SpeciesPrice> speciesPrices)
        {
            /// TODO: implement this
            return Task.FromResult(true);
        }

        public Task<Biomass> GetBiomassAsync()
        {
            var response = new Biomass()
            {
                MeasurementUnit = "kg"
            };
            response.BiomassGrids.Add(new BiomassGrid()
            {
                SpeciesCode = "PIL"
            });
            response.BiomassGrids[0].BiomassCells.Add(new BiomassCell()
            {
                Longitude = 1.6877561f,
                Latitude = 40.901618f,
                Biomass = 1000.0f
            });
            response.BiomassGrids.Add(new BiomassGrid()
            {
                SpeciesCode = "BOG"
            });
            response.BiomassGrids[1].BiomassCells.Add(new BiomassCell()
            {
                Longitude = 1.6170411f,
                Latitude = 40.801618f,
                Biomass = 2500.0f
            });

            return Task.FromResult(response);
        }

        public Task<List<SalesSummary>> GetSalesSummariesAsync(DateTime start, DateTime end)
        {
            var response = new List<SalesSummary>()
            {
                new SalesSummary()
                {
                    MarketId = "Market1",
                    MeasurementUnit = "kg",
                    Currency = "EUR",
                    Sales = new List<Sale>()
                    {
                        new Sale()
                        {
                            SpeciesCode = "PIL",
                            Quantity = 23,
                            Value = 232.3
                        },
                        new Sale()
                        {
                            SpeciesCode = "BOG",
                            Quantity = 12,
                            Value = 123.4
                        }
                    }
                },
                new SalesSummary()
                {
                    MarketId = "Market2",
                    MeasurementUnit = "kg",
                    Currency = "EUR",
                    Sales = new List<Sale>()
                    {
                        new Sale()
                        {
                            SpeciesCode = "PIL",
                            Quantity = 234,
                            Value = 532.3
                        },
                        new Sale()
                        {
                            SpeciesCode = "BOG",
                            Quantity = 132,
                            Value = 223.4
                        }
                    }
                },
            };
            return Task.FromResult(response);

        }

        public Task<CatchDispositionSummary> GetCatchDispositionSummaryAsync(DateTime start, DateTime end)
        {
            var response = new CatchDispositionSummary()
            {
                MeasurementUnit = "kg",
                DispositionGrids = new List<DispositionGrid>()
                {
                    new DispositionGrid()
                    {
                        GearCode = "Gear1",
                        SpeciesCode = "PIL",
                        DispositionCells = new List<DispositionCell>()
                        {
                            new DispositionCell()
                            {
                                GrossCatchBiomass = 5000.0f,
                                LiveDiscardsBiomass = 1000.0f,
                                DeadDiscardsBiomass = 2000.0f,
                                Latitude = 40.901618f,
                                Longitude = 1.6877561f
                            },
                            new DispositionCell()
                            {
                                GrossCatchBiomass = 3000.0f,
                                LiveDiscardsBiomass = 500.0f,
                                DeadDiscardsBiomass = 1000.0f,
                                Latitude = 40.801618f,
                                Longitude = 1.6170411f
                            }
                        }
                    },
                    new DispositionGrid()
                    {
                        GearCode = "Gear2",
                        SpeciesCode = "BOG",
                        DispositionCells = new List<DispositionCell>()
                        {
                            new DispositionCell()
                            {
                                GrossCatchBiomass = 7000.0f,
                                LiveDiscardsBiomass = 1500.0f,
                                DeadDiscardsBiomass = 2500.0f,
                                Latitude = 40.901618f,
                                Longitude = 1.6877561f
                            },
                            new DispositionCell()
                            {
                                GrossCatchBiomass = 4000.0f,
                                LiveDiscardsBiomass = 800.0f,
                                DeadDiscardsBiomass = 1200.0f,
                                Latitude = 40.801618f,
                                Longitude = 1.6170411f
                            }
                        }
                    }
                }
            };
            return Task.FromResult(response);
        }

        public Task<bool> UpdateCatchDispositionSummaryAsync(CatchDispositionSummary catchDispositionSummary)
        {
            /// TODO: implement this
            return Task.FromResult(true);
        }
        #endregion // Internals
    }
}
