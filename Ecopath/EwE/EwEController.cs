using Ecopath.Models;
using EwECore;
using EwEPlugin;
using EwEUtils.Core;
using EwEBridge;
using static EwECore.cCore;
using EwEBridge.Ecospace;

namespace Ecopath.EwE
{
    // ToDo: devise a mechanism to bridge time step sizes; right now the code assumes that time steps are monthly

    public class EwEController : IEwEController
    {
        #region Private vars 

        /// <summary>The <see cref="cCore"/> to operate on.</summary>
        private readonly cCore m_core;
        /// <summary>The Ecospace run thread, if any.</summary>
        private Thread? m_thread;
        /// <summary>Core message handler for tracking EwE execution flow.</summary>
        private cMessageHandler? m_mh;

        private readonly ILogger<EwEController> m_logger;
        private EwEConfiguration? m_configuration;

        private RunStates m_runstate = RunStates.idle;

        /// <summary>Event for internal state monitoring.</summary>
        private event Action<RunStates>? OnRunStateChanged;

        // --- Cached run info
        private List<SpeciesPrice>? m_prices;
        private Biomass? m_biomass;
        private SalesSummary? m_salessummary;

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
            /// <summary>Waiting for external input.</summary>
            waiting,
            /// <summary>Busy running simulations.</summary>
            running,
            /// <summary>Busy stopping.</summary>
            stopping
        }

        public EwEController(ILogger<EwEController> logger)
        {

            this.m_core = new cCore();
            cLog.VerboseLevel = eVerboseLevel.Disabled; // Turn off all internal event logging
            this.RunState = RunStates.idle;

            this.m_mh = new cMessageHandler(OnCoreMessage, eCoreComponentType.Ecospace, eMessageType.EcospaceRunCompleted, SynchronizationContext.Current);
            this.m_core.Messages.AddMessageHandler(m_mh);

            // To make sure we can find local resources. This is rather hack.
            Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
            this.m_logger = logger;

            this.m_core.PluginManager = new cPluginManager();
            this.m_logger.LogInformation("EwE loaded {0} plug-in(s)", this.m_core.PluginManager.LoadPlugins());

            IPlugin? pi = GetPlugin(typeof(EwEBridge.Ecospace.cEcospaceBridgePlugin));
            if (pi != null)
            {
                cEcospaceBridgePlugin ppt = (cEcospaceBridgePlugin)pi;
                ppt.BridgeCallback = this.BridgeCallback;
            }
        }

        ~EwEController()
        {
            this.m_core.Messages.RemoveMessageHandler(m_mh);
            this.m_mh = null;

            this.ForceStop();

            this.m_core.CloseModel();
            this.m_core.Dispose();
        }

        #region Public interaction 

        /// <summary>
        /// The current EwE run state.
        /// </summary>
        public RunStates RunState
        {
            get => this.m_runstate;
            private set
            {
                if (this.m_runstate != value)
                {
                    this.m_runstate = value;
                    this.OnRunStateChanged?.Invoke(this.m_runstate);
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

            this.RunState = RunStates.starting;
            this.m_configuration = config;

            if (!File.Exists(this.m_configuration.ModelName))
            {
                throw new FileNotFoundException("EwE model file '{0}' cannot be found", this.m_configuration.ModelName); 
            }

            if (!this.m_core.LoadModel(m_configuration.ModelName))
            {
                throw new Exception($"EwE could not load model '{this.m_configuration.ModelName}'");
            }
            this.m_logger.LogInformation("EwE - Ecopath loaded model '{0}'", this.m_configuration.ModelName);

            bool bIsBalanced = false;
            if (!this.m_core.RunEcopath(ref bIsBalanced) | !bIsBalanced)
            {
                throw new Exception("EwE - Ecopath does not balance");
            }
            this.m_logger.LogInformation("EwE - Ecopath does balance");

            if (this.m_configuration.EcosimScenario <= 0 | !this.m_core.LoadEcosimScenario(m_configuration.EcosimScenario))
            {
                throw new Exception($"EwE - Ecosim scenario {this.m_configuration.EcosimScenario} not loaded");
            }
            this.m_logger.LogInformation("EwE - Ecosim scenario {0} loaded", this.m_configuration.EcosimScenario);

            if (this.m_configuration.EcosimTimeSeries > 0)
            {
                if (!this.m_core.LoadTimeSeries(m_configuration.EcosimTimeSeries))
                {
                    throw new Exception($"EwE - Ecosim time series {this.m_configuration.EcosimTimeSeries} not loaded");
                }
                this.m_logger.LogInformation("EwE - Ecosim time series {0} loaded", this.m_configuration.EcosimTimeSeries);
            }

            cEcoSimModelParameters parms = this.m_core.EcosimModelParameters;
            parms.NumberYears = this.m_configuration.MaxRunYears; // No of years apply to both Sim and Space

            if (!this.m_core.RunEcosim())
            {
                throw new Exception("EwE - Ecosim failed to run");
            }
            this.m_logger.LogInformation("EwE - Ecosim run successfully");

            if (this.m_configuration.EcospaceScenario <= 0 | !this.m_core.LoadEcospaceScenario(this.m_configuration.EcospaceScenario))
            {
                throw new Exception($"EwE - Ecospace scenario {this.m_configuration.EcospaceScenario} not loaded");
            }
            this.m_logger.LogInformation("EwE - Ecospace scenario {0} loaded", this.m_configuration.EcospaceScenario);

            cEcospaceDataStructures ds = this.m_core.EcospaceDataStructures;
            ds.SpinUpYears = this.m_configuration.SpinupYears;
            ds.UseSpinUp = (this.m_configuration.SpinupYears > 0);
            this.m_logger.LogInformation("EwE - Ecospace spin-up {0}", ds.UseSpinUp ? this.m_configuration.SpinupYears.ToString() : "off"); 

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
            this.m_thread = new Thread(RunEcospace);
            this.m_thread.Start();

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            OnRunStateChanged -= Handler;

            return (RunState == RunStates.waiting) ? 1 : -1;
        }

        /// <summary>
        /// We might as well make this an async method too, even though there won't be any waiting
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ContinueAsync(int timeoutMs = 60000)
        {
            if (this.RunState != RunStates.waiting) return false;
    
            // Need to wait for RunState to switch back to Waiting. Only return after
            var tcs = new TaskCompletionSource();

            void Handler(RunStates state)
            {
                if (state == RunStates.waiting)
                {
                    tcs.TrySetResult();
                }
            }
            this.OnRunStateChanged += Handler;

            // Carry on
            this.m_core.EcospacePaused = false;
            this.RunState = RunStates.running;

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            this.OnRunStateChanged -= Handler;

            this.m_logger.LogInformation("EwE - continue");
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

            this.OnRunStateChanged += Handler;

            this.m_core.StopEcospace(); // Initiate graceful shutdown

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
            this.OnRunStateChanged -= Handler;
            this.ForceStop();
            return false;
        }

        public Task<bool> UpdatePricesAsync(List<SpeciesPrice> speciesPrices)
        {
            this.m_prices = speciesPrices;
            return Task.FromResult(true);
        }

        public Task<Biomass> GetBiomassAsync()
        {
            if (this.m_biomass == null)
                this.m_biomass = new Biomass() { MeasurementUnit = "kg" };
            return Task.FromResult(this.m_biomass);
        }

        void Clear()
        {
            this.m_prices = null;
            this.m_biomass = null;
        }

        /// <summary>
        /// Prepare a snapshot of the biomasses of the current time step.
        /// </summary>
        void BuildTimeStepCache()
        {
            // ToDo: add critcal section?

            // Wipe
            this.m_biomass = new Biomass()
            {
                MeasurementUnit = "kg"
            };
            if (m_configuration != null)
            {
                cEcospaceDataStructures ds = this.m_core.EcospaceDataStructures;
                cEcospaceBasemap bm = this.m_core.EcospaceBasemap;

                foreach (string spp in m_configuration.SpeciesOfInterest())
                {
                    BiomassGrid grid = new BiomassGrid()
                    {
                        // Also add projection
                        SpeciesCode = spp
                    };
                    int iGroup = m_configuration.get_SpeciesGroup(spp);
                    Single sppProp = m_configuration.get_SpeciesContribution(spp); // Also need to correct for cell area, expected kg

                    for (int ic = 1; ic <= ds.InCol; ic++)
                        for (int ir = 1; ir <= ds.InRow; ir++)
                            if (bm.IsModelledCell(ir, ic))
                            {
                                grid.BiomassCells.Add(new BiomassCell()
                                {
                                    Latitude = bm.RowToLat(ir),
                                    Longitude = bm.ColToLon(ic),
                                    // Biomass corrected by group proportion, area, and annual -> monthly rates
                                    Biomass = ds.Bcell[ir, ic, iGroup] * sppProp * ds.CellArea[ir, ic] / cCore.N_MONTHS
                                });
                            }
                    this.m_biomass.BiomassGrids.Add(grid);
                }
            }
        }

        public Task<List<SalesSummary>> GetSalesSummariesAsync(DateTime start, DateTime end)
        {
            // ToDo: validate if the time step falls within the indicated time span
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

        #endregion // Public interaction

        #region Internals

        private void RunEcospace()
        {
            cCore.EcoSpaceInterfaceDelegate dgt = new EcoSpaceInterfaceDelegate(EcospaceCallBack);
            this.m_core.RunEcospace(ref dgt);
        }

        private void EcospaceCallBack(ref cEcospaceTimestep timestep)
        {
            if (this.RunState == RunStates.stopping) return;

            // Do not halt while in spin-up
            cEcospaceDataStructures ds = m_core.EcospaceDataStructures;
            if (ds.bInSpinUp) return;
            if (timestep.TimeStepinYears < m_configuration?.StartYear) return;
            //if (_core.EcosimFirstYear() + timestep.TimeStepinYears < _configuration?.StartYear) return; // Should use absolute start year instead; is more robust

            this.m_logger.LogInformation(string.Format("EwE - pausing at timestep {0}, {1}", timestep.iTimeStep, m_core.EcospaceTimestepToAbsoluteTime(timestep.iTimeStep)));

            this.RunState = RunStates.waiting;
            this.m_core.EcospacePaused = true;
        }

        private void OnCoreMessage(ref cMessage msg)
        {
            switch (msg.Type)
            {
                case eMessageType.EcospaceRunCompleted:

                    // Clear all modifications made by the process
                    this.m_core.DiscardChanges();
                    // Correctly reset the state and clean up
                    this.RunState = RunStates.idle;
                    this.m_thread = null;
                    break;
            }

        }

        private void ForceStop()
        {
            try
            {
                if (this.m_thread != null && m_thread.IsAlive)
                    this.m_thread.Interrupt();
            }
            catch (Exception ex)
            {
                //_logger.LogInformation("EwE - exception ...");
            }

            this.RunState = RunStates.idle; // Manually reset to idle if needed
        }

        public IPlugin? GetPlugin(Type t)
        { 
            cPluginManager pm = this.m_core.PluginManager;
            List<IPlugin> plugins = (List<IPlugin>)pm.GetPlugins(t);
            if (plugins.Count > 0)
                return plugins[0];
            return null;
        }

        private void BridgeCallback(cEcospaceBridgePlugin.EventType e, int iTime)
        {
            try
            {
                switch (e)
                {
                    case cEcospaceBridgePlugin.EventType.None:
                        break; // NOP
                    case cEcospaceBridgePlugin.EventType.BeginTimeStep:
                        // Integrate prices
                        break;
                    case cEcospaceBridgePlugin.EventType.BeginTimeStepPost:
                        break;
                    case cEcospaceBridgePlugin.EventType.EndTimeStep:
                            // Gather relevant output info
                            this.BuildTimeStepCache();
                        break;
                    case cEcospaceBridgePlugin.EventType.EndTimeStepPost:
                        break;
                    case cEcospaceBridgePlugin.EventType.EffortDistrPost:
                        break;
                    default:
                        break; // NOP
                }
            }
            catch (Exception ex)
            {
                this.m_logger.LogInformation("EwE - exception {0} on bridgecallback {1}", ex.Message, e.ToString());
            }

        }

        #endregion // Internals
    }
}
