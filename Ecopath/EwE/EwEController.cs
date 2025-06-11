using Ecopath.Models;
using EwEBridge.Ecospace;
using EwECore;
using EwEPlugin;
using EwEUtils.Core;
using System.Diagnostics;

namespace Ecopath.EwE
{
    // About the flow of Ecospace and this controller:
    // This code relies on a bridge to respond to EwE plug-in points, and the Ecospace pause mechanism to halt timestepping
    // It is important to know that the Ecospace Pause mechanism waits at the BEGINNING of a new time step
    //
    // This has somewhat counterintuitive consequences:
    // - Ecospace biomass, catch and other end-of timestep data is gathered at the end of a timestep
    // - Ecospace then pauses at the beginning of a new timestep for POSEIDON to provide catch dispositions
    // - The catch dispositions are injected back into Ecospace as soon as the time step resumes: at the start of the next time step
    // This means that in the interim, Ecospace biomasses are not up to date. That does not matter as no other interactivity with Ecospace is allowed.
    // We'll be confused plenty later.

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

        // --- Data in and out 
        private List<SpeciesPrice>? m_pricesIn;
        private CatchDispositionSummary? m_catchIn;

        private Biomass? m_biomassOut;
        private CatchDispositionSummary? m_catchOut;
        private List<SalesSummary> m_salesOut = new();

        #endregion // Private vars 

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

            IPlugin? pi = GetPlugin(typeof(cEcospaceBridgePlugin));
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

        #region EwE helpers

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

        #endregion EwE helpers

        #region Framework interactions

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
                    tcs.TrySetResult();
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
            this.m_pricesIn = speciesPrices;
            return Task.FromResult(true);
        }

        public Task<bool> UpdateCatchDispositionSummaryAsync(CatchDispositionSummary catchDispositionSummary)
        {
            this.m_catchIn = catchDispositionSummary;
            return Task.FromResult(true);
        }

        public Task<Biomass> GetBiomassAsync()
        {
            if (this.m_biomassOut == null)
                this.m_biomassOut = new Biomass() { MeasurementUnit = "kg" };
            return Task.FromResult(this.m_biomassOut);
        }

        public Task<List<SalesSummary>> GetSalesSummariesAsync(DateTime start, DateTime end)
        {
          return Task.FromResult(this.m_salesOut);
        }

        public Task<CatchDispositionSummary> GetCatchDispositionSummaryAsync(DateTime start, DateTime end)
        {
            if (this.m_catchOut == null)
                this.m_catchOut = new CatchDispositionSummary() { MeasurementUnit = "kg" };
            return Task.FromResult(this.m_catchOut);
        }

        #endregion // Public interaction

        #region Data interactions

        private void Clear()
        {
            this.m_pricesIn = null;
            this.m_catchIn = null;

            this.m_catchOut = null;
            this.m_biomassOut = null;
        }

        private void IntegratePrices()
        {
            if (this.m_pricesIn == null) return;
            if (this.m_configuration == null) return;

            var ds = this.m_core.EcopathDataStructures;

            foreach (var price in m_pricesIn)
            {
                // Fleet is identified by gear code + marketcode, not PortCode
                    
                // ToDo_JS: activate code below
                int iFleet = 42; // this.m_configuration.get_GearFleet(price.GearCode, price.MarketCode);
                int iGroup = this.m_configuration.get_SpeciesGroup(price.SpeciesCode);
                float pr = (float)price.Price;

                // ToDo: implement unit conversions?
                Debug.Assert(string.Compare(price.Currency, "eur", true) == 0);
                Debug.Assert(string.Compare(price.MeasuremenyUnit, "kg", true) == 0);

                if (iFleet > 0 && iGroup > 0)
                    ds.Market[iFleet, iGroup] = (float)price.Price;
                else
                {
                    // ToDo_JS: decide how to respond to a potential EwE misconfiguration.
                    //this.m_logger.LogWarning("Price record gear '{0}', market '{1}', species '{2}' cannot be mapped to EwE", price.GearCode, price.marketCode, price.SpeciesCode), price);
                    //throw new Exception("Price record gear '{0}', market '{1}', species '{2}' cannot be mapped to EwE", price.GearCode, price.marketCode, price.SpeciesCode);
                }
            }

            // Done, clear buffer. Prices within EwE will remain fixed until the next change
            this.m_pricesIn = null;
        }

        private void IntegrateCatchDispositions()
        {
            if (this.m_catchIn == null) return;
            if (this.m_configuration == null) return;

            var ds = this.m_core.EcospaceDataStructures;
            var bm = this.m_core.EcospaceBasemap;
            
            foreach (var grid in this.m_catchIn.DispositionGrids)
            {
                // Impact standing biomass. This data arrives too late to update Ecospace results.
                // Only correct standing biomasses for now.
                int iGroup = this.m_configuration.get_SpeciesGroup(grid.SpeciesCode);
                foreach (var cell in grid.DispositionCells)
                {
                    int ir = (int)Math.Floor(bm.LatToRow((float)cell.Latitude));
                    int ic = (int)Math.Floor(bm.LonToCol((float)cell.Longitude));

                    if (1 <= ir & ir <= ds.InRow & 1 <= ic & ic <= ds.InCol)
                    {
                        double loss = cell.GrossCatchBiomass - cell.LiveDiscardsBiomass;
                        float dens = KgToDensity(loss, ir, ic);
                        float available = ds.Bcell[ir, ic, iGroup];

                        Debug.Assert(loss >= 0, "Cannot fish negatively. Would be nice, but sorry, no.");
                        Debug.Assert(dens > available, "Not enough B in cell to satisfy fishing");
                        Debug.Assert(ds.Depth[ir, ic] > 0, "Not a modelled cell?!");

                        if (dens > available)
                        {
                            // WHoah!! External fishing is catching more than is available in this cell
                            throw new Exception(string.Format("EwE controller cannot integrate Catch Disposition {0} kg ({1} t/km2), into cell {2}x{3} ({4}x{5}), only {6} t/km2 available in Ecospace",
                                loss, dens, cell.Longitude, cell.Latitude, ic, ir, available));
                        }

                        // Leave some tiny biomass in the cell; fisheries cannot catch it all (and Ecospace does not like divisions by zero)
                        ds.Bcell[ir, ic, iGroup] = (float)Math.Max(1E-10, available - dens);
                    }
                    else
                    {
                        // Cell out of bounds. Ignore.
                    }
                }
            }
            this.m_catchIn = null;
        }

        /// <summary>
        /// Prepare a snapshot of the biomasses of the current time step.
        /// </summary>
        private void CacheBiomassData()
        {
            // Wipe
            this.m_biomassOut = new Biomass()
            {
                MeasurementUnit = "kg"
            };
            if (this.m_configuration != null)
            {
                cEcospaceDataStructures ds = this.m_core.EcospaceDataStructures;
                cEcospaceBasemap bm = this.m_core.EcospaceBasemap;

                foreach (string spp in this.m_configuration.SpeciesCodes())
                {
                    BiomassGrid grid = new BiomassGrid()
                    {
                        // Also add projection?
                        SpeciesCode = spp
                    };
                    int iGroup = this.m_configuration.get_SpeciesGroup(spp);
                    Single sppProp = this.m_configuration.get_SpeciesContribution(spp);

                    for (int ic = 1; ic <= ds.InCol; ic++)
                        for (int ir = 1; ir <= ds.InRow; ir++)
                            if (ds.Depth[ir, ic] > 0)
                            {
                                grid.BiomassCells.Add(new BiomassCell()
                                {
                                    Latitude = bm.RowToLat(ir),
                                    Longitude = bm.ColToLon(ic),
                                    // Express biomass of group proportion in kg at timestep units (not annual)
                                    Biomass = DensityToKg(ds.Bcell[ir, ic, iGroup], ir, ic) * sppProp * ds.TimeStep
                                });
                            }
                    this.m_biomassOut.BiomassGrids.Add(grid);
                }
            }
        }

        /// <summary>
        /// Prepare a snapshot of catch data for export. Only include internal gears, e.g., of catches produced by EwE.
        /// </summary>
        private void CacheCatchAndSalesData()
        {
            if (this.m_catchOut == null)
                this.m_catchOut = new() { MeasurementUnit = "kg" };
            else
                this.m_catchOut.DispositionGrids.Clear();

            if (this.m_salesOut == null)
                this.m_salesOut = new() { };
            else
                this.m_salesOut.Clear();

            if (this.m_configuration == null) return;

            // For summing up sales
            Dictionary<DualKey, double> salesVolume = new();
            Dictionary<DualKey, double> salesValue = new();

            cEcopathDataStructures ecopathds = this.m_core.EcopathDataStructures;
            cEcospaceDataStructures spaceds = this.m_core.EcospaceDataStructures;
            cEcospaceBasemap bm = this.m_core.EcospaceBasemap;
            List<int> fished = new();

            for (int iGroup = 1; iGroup <= this.m_core.nGroups; iGroup++)
                if (this.m_core.get_EcopathGroupInputs(iGroup).IsFished)
                    fished.Add(iGroup);

            foreach (int iGroup in fished)
            {
                string speccode = this.m_configuration.get_GroupSpecies(iGroup);
                var sppProp = this.m_configuration.get_SpeciesContribution(speccode);

                foreach (string gearcode in this.m_configuration.GearCodes())
                {
                    // Is fleet managed by EwE?
                    if (this.m_configuration.get_ExternalGear(gearcode) == false)
                    {
                        // Tally up the catch dispositions for all the markets this gear code caters to
                        string[] markets = this.m_configuration.MarketCodes(gearcode);

                        double[,] catches = new double[spaceds.InRow + 1, spaceds.InCol + 1];
                        double[,] deaddisc = new double[spaceds.InRow + 1, spaceds.InCol + 1];
                        double[,] livedisc = new double[spaceds.InRow + 1, spaceds.InCol + 1];
                        bool bHasData = false;

                        foreach (string marketcode in markets)
                        {
                            int iFleet = this.m_configuration.get_GearFleet(gearcode, marketcode);
                            if (ecopathds.Landing[iFleet, iGroup] + ecopathds.Discard[iFleet, iGroup] > 0)
                                for (int ir = 1; ir <= spaceds.InRow; ir++)
                                    for (int ic = 1; ic <= spaceds.InCol; ic++)
                                        if (spaceds.Depth[ir, ic] > 0)
                                        {
                                            // Convert EwE annual densities to monthly absolutes
                                            double cellCatchesAbs = DensityToKg(spaceds.CatchGroupFleetMap[iFleet, iGroup][ir, ic], ir, ic) * sppProp * spaceds.TimeStep;
                                            double cellLiveDiscAbs = DensityToKg(spaceds.DiscardSurviveGroupFleetMap[iFleet, iGroup][ir, ic], ir, ic) * sppProp * spaceds.TimeStep;
                                            double cellDeadDiscAbs = DensityToKg(spaceds.DiscardMortGroupFleetMap[iFleet, iGroup][ir, ic], ir, ic) * sppProp * spaceds.TimeStep;

                                            // Tally sales
                                            DualKey dk = DualKey.Make(marketcode, speccode);
                                            if (!salesVolume.ContainsKey(dk))
                                            {
                                                salesVolume[dk] = 0;
                                                salesValue[dk] = 0;
                                            }
                                            salesVolume[dk] += (cellCatchesAbs - cellDeadDiscAbs);
                                            salesValue[dk] += (cellCatchesAbs - cellDeadDiscAbs) * ecopathds.Market[iFleet, iGroup];

                                            // Prepare catch deposition. Note that EwE catches do NOT include live discards
                                            // ToDo_JS: Decide on the below. What is the framework expecting? 
                                            // cellCatchesAbs += cellLiveDiscAbs;

                                            catches[ir, ic] += cellCatchesAbs;
                                            livedisc[ir, ic] += cellLiveDiscAbs;
                                            deaddisc[ir, ic] += cellDeadDiscAbs;
                                            bHasData = true;
                                        }
                        }

                        // Finally prepare data for the framework
                        if (bHasData)
                        {
                            // Prepare disposition grid
                            var grid = new DispositionGrid() { GearCode = gearcode, SpeciesCode = speccode };
                            for (int ir = 1; ir <= spaceds.InRow; ir++)
                                for (int ic = 1; ic <= spaceds.InCol; ic++)
                                    if (spaceds.Depth[ir, ic] > 0)
                                    {
                                        grid.DispositionCells.Add(new DispositionCell()
                                        {
                                            Latitude = bm.RowToLat(ir),
                                            Longitude = bm.ColToLon(ic),
                                            // Express catch stats of group proportion in kg at timestep units (not annual)

                                            GrossCatchBiomass = catches[ir, ic],
                                            LiveDiscardsBiomass = livedisc[ir, ic],
                                            DeadDiscardsBiomass = deaddisc[ir, ic]
                                        });
                                    }
                            this.m_catchOut.DispositionGrids.Add(grid);
                        }
                    }
                }
            }

            // Prepare sales
            foreach (string marketcode in this.m_configuration.MarketCodes())
            {
                var sales = new SalesSummary()
                {
                    MarketId = marketcode,
                    MeasurementUnit = "kg",
                    Currency = "EUR", // No conversion here
                    Sales = new List<Sale>()
                };
                foreach (string speccode in this.m_configuration.SpeciesCodes())
                {
                    DualKey dk = DualKey.Make(marketcode, speccode);
                    if (salesVolume.ContainsKey(dk))
                    {
                        Sale s = new Sale()
                        {
                            SpeciesCode = speccode,
                            Quantity = salesVolume[dk],
                            Value = salesValue[dk]
                        };
                        salesVolume.Remove(dk);
                        salesValue.Remove(dk);
                        sales.Sales.Add(s);
                    }
                }
                this.m_salesOut.Add(sales);
            }

            // Sanity check
            if (salesValue.Keys.Count() > 0)
            {
                throw new Exception("There are {0} unexpected sales record(s). Please kick the EwE developers.");
            }
        }

        /// <summary>
        /// Maps Ecospace currency tonnes.km-2 to kg.
        /// </summary>
        /// <param name="dens"></param>
        /// <param name="irow"></param>
        /// <param name="icol"></param>
        /// <returns></returns>
        private double DensityToKg(float dens, int irow, int icol)
        {
            // ToDo_JS: validate model currency unit (And yes, "currency" is biomass unit. Nothing to do with money. Fun times)
            return dens * 1000 * this.m_core.EcospaceDataStructures.CellArea[irow, icol];
        }

        /// <summary>
        /// Maps kg to Ecospace currency tonnes.km-2
        /// </summary>
        /// <param name="kg"></param>
        /// <param name="irow"></param>
        /// <param name="icol"></param>
        /// <returns></returns>
        private float KgToDensity(double kg, int irow, int icol)
        {
            // ToDo_JS: validate actual model currency unit
            float area = this.m_core.EcospaceDataStructures.CellArea[irow, icol];
            if (area == 0) area = 1; // Can happen
            return (float) kg / (area * 1000);
        }

        #endregion // Data interactions

        #region Internal - EwE interactions

        private void RunEcospace()
        {
            cCore.EcoSpaceInterfaceDelegate? dgt = null;
            this.m_core.RunEcospace(ref dgt);
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

        public IPlugin? GetPlugin(System.Type t)
        { 
            cPluginManager pm = this.m_core.PluginManager;
            List<IPlugin> plugins = (List<IPlugin>)pm.GetPlugins(t);
            if (plugins.Count > 0)
                return plugins[0];
            return null;
        }

        private void BridgeCallback(cEcospaceBridgePlugin.EventType e, int iTime)
        {
            if (this.RunState == RunStates.stopping) return;

            try
            {
                switch (e)
                {
                    case cEcospaceBridgePlugin.EventType.None:
                        // NOP
                        break; 

                    case cEcospaceBridgePlugin.EventType.BeginTimeStep:
                        this.IntegratePrices();
                        this.IntegrateCatchDispositions();
                        break;

                    case cEcospaceBridgePlugin.EventType.BeginTimeStepPost:
                        // NOP
                        break;

                    case cEcospaceBridgePlugin.EventType.EndTimeStep:

                        // Prepare data for sending out
                        this.CacheBiomassData();
                        this.CacheCatchAndSalesData();

                        // Handle pause timer
                        if (this.MustPause(iTime))
                        {
                            this.m_logger.LogInformation("EwE - pausing at timestep {0}", iTime);
                            this.RunState = RunStates.waiting;
                            this.m_core.EcospacePaused = true;
                        }
                        break;

                    case cEcospaceBridgePlugin.EventType.EffortDistrPost:
                        // NOP
                        break;

                    default:
                        // NOP
                        break; 
                }
            }
            catch (Exception ex)
            {
                this.m_logger.LogInformation("EwE - exception {0} on bridgecallback {1}", ex.Message, e.ToString());
            }

        }

        private bool MustPause(int iTime)
        {
            // Do not halt while in spin-up
            cEcospaceDataStructures ds = m_core.EcospaceDataStructures;
            if (ds.bInSpinUp) return false;
            DateTime dt = this.m_core.EcospaceTimestepToAbsoluteTime(iTime);
            return (dt.Year >= m_configuration?.StartYear);
        }

        #endregion // Internal - EwE interactions
    }
}
