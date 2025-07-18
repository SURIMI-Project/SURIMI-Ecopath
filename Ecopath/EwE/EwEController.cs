using Ecopath.Models;
using EwEBridge.Ecospace;
using EwECore;
using EwEPlugin;
using EwEUtils.Core;
using System.Diagnostics;
using System.Globalization;

namespace Ecopath.EwE
{
    // About the flow of Ecospace and this controller:
    // - The aim was to insert agent-based fisheries into EwE with a minimal code changes
    // - EwE needs to account for this fishing in the running model data, but also in the various result arrays
    //
    // This implementation relies on a plug-in bridge (to supercharge EwE interop) and the 'off-the-shelf' Ecospace pause mechanism
    // * It is important to know that the Ecospace Pause mechanism waits at the BEGINNING of a time step
    // - Ecospace biomass, catch, sales and other end-of timestep data is gathered at the end of a timestep
    // - Ecospace then pauses at the beginning of a new timestep for POSEIDON to provide catch dispositions
    // - The catch dispositions are injected back into Ecospace in the middle of a time step, when effort has been distributed and before EwE starts fishing
    //
    // This was achieved with minimal interference in the EwE code:
    // - The ONLY changes made to the EwE core entailed clearing out time step results BEFORE the 
    // * This means that EwE fishes after POSEIDON (EwE fishes on the left-overs). EwE and POSEIDON currently do not fish together

    // 16 Jun 25 (Nicolas visit)
    // V MultiStanza: properly encode fish sizes, gear nationalities, and other refinements. The current coding system is not up to par
    // V We now properly fish! Data integration performed in the middle of the Ecospace time step, using catch dispositions received earlier

    // General things to do:
    // ! devise a mechanism to bridge time step sizes; right now the code assumes that time steps are monthly
    // ! expand entity matching logic
    // ! devise system to order up the same scenario across all participating models
    // ! user stories in GitHub!!!

    public class EwEController : IEwEController
    {
        #region Private vars 

        /// <summary>The <see cref="cCore"/> to operate on.</summary>
        private readonly cCore m_core;
        /// <summary>The Ecospace run thread, if any.</summary>
        private Thread? m_thread;
        /// <summary>The framework logger. Very pretty.</summary>
        private readonly ILogger<EwEController> m_logger;
        /// <summary>EwE configuration that defines how EwE entities relate to common concepts (species, fishing, markets, etc).</summary>
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

        // --- Internal tracking
        private int m_nSpinUpSteps = 0;
        private int m_iSpinUpStep = 0;

        /// <summary>
        /// To track species group proportions affected by external fishing
        /// </summary>
        private Dictionary<int, GroupSpeciesProportions> m_groupSpeciesProportions = new();

        #endregion // Private vars 

         public EwEController(ILogger<EwEController> logger)
        {

            this.m_core = new cCore();
            cLog.VerboseLevel = eVerboseLevel.Disabled; // Turn off all internal event logging
            this.RunState = RunStates.idle;

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
                    //Console.WriteLine("Run state set to " + value.ToString());
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
        public async Task<int> StartAsync(EwEConfiguration config, int timeoutMs = 60 * 10 * 1000)
        {
            // Check readiness
            if (RunState != RunStates.idle)
                throw new Exception("EwE controller already busy, aborting");

            // Commence configuration
            this.RunState = RunStates.starting;
            this.m_configuration = config;

            // Load model
            if (!File.Exists(this.m_configuration.ModelName))
                throw new FileNotFoundException("EwE model file '{0}' cannot be found", this.m_configuration.ModelName); 
            if (!this.m_core.LoadModel(m_configuration.ModelName))
                throw new Exception($"EwE could not load model '{this.m_configuration.ModelName}'");
            this.m_logger.LogInformation("EwE - Ecopath loaded file '{0}', model '{1}'", this.m_configuration.ModelName, this.m_core.EcopathDataStructures.ModelName);

            // Check Ecopath balancing
            bool bIsBalanced = false;
            if (!this.m_core.RunEcopath(ref bIsBalanced) | !bIsBalanced)
                throw new Exception("EwE - Ecopath does not balance");
            this.m_logger.LogInformation("EwE - Ecopath does balance");

            // Check and load Ecosim
            if (this.m_configuration.EcosimScenario <= 0 | !this.m_core.LoadEcosimScenario(m_configuration.EcosimScenario))
                throw new Exception($"EwE - Ecosim scenario {this.m_configuration.EcosimScenario} not loaded");
            this.m_logger.LogInformation("EwE - Ecosim scenario {0} loaded", this.m_configuration.EcosimScenario);
            if (this.m_configuration.EcosimTimeSeries > 0)
            {
                if (!this.m_core.LoadTimeSeries(m_configuration.EcosimTimeSeries))
                    throw new Exception($"EwE - Ecosim time series {this.m_configuration.EcosimTimeSeries} not loaded");
                this.m_logger.LogInformation("EwE - Ecosim time series {0} loaded", this.m_configuration.EcosimTimeSeries);
            }

            // Configure Ecosim
            cEcoSimModelParameters parms = this.m_core.EcosimModelParameters;
            parms.NumberYears = this.m_configuration.MaxRunYears; // No of years apply to both Sim and Space

            // Run Ecosim
            this.m_logger.LogInformation("EwE - Going to run Ecosim for {0} years", parms.NumberYears);
            if (!this.m_core.RunEcosim())
                throw new Exception("EwE - Ecosim failed to run");
            this.m_logger.LogInformation("EwE - Ecosim run successfully");

            // Check and load Ecospace
            if (this.m_configuration.EcospaceScenario <= 0 | !this.m_core.LoadEcospaceScenario(this.m_configuration.EcospaceScenario))
                throw new Exception($"EwE - Ecospace scenario {this.m_configuration.EcospaceScenario} not loaded");
            this.m_logger.LogInformation("EwE - Ecospace scenario {0} loaded", this.m_configuration.EcospaceScenario);

            // Now load the configuration
            this.m_configuration.Load(this.m_core);
            this.m_logger.LogInformation("EwE - exposed {0} group(s), {1} fleet(s) and {2} market(s) to gRPC",
                this.m_configuration.Mappings(KeyDomain.Species).Count(), 
                this.m_configuration.Mappings(KeyDomain.FleetSegment).Count(), 
                this.m_configuration.Mappings(KeyDomain.Market).Count());

            // Build species proportion accounting
            foreach (int iGroup in m_configuration.FishedGroups)
                this.m_groupSpeciesProportions[iGroup] = GroupSpeciesProportionsFactory.Create(this.m_core, iGroup, m_configuration.Mappings(KeyDomain.Species));

            // Configure Ecospace
            cEcospaceDataStructures ds = this.m_core.EcospaceDataStructures;
            ds.SpinUpYears = this.m_configuration.SpinupYears;
            ds.UseSpinUp = (this.m_configuration.SpinupYears > 0);
            this.m_logger.LogInformation("EwE - Ecospace spin-up for {0} years", ds.UseSpinUp ? this.m_configuration.SpinupYears.ToString() : "off");

            // Start running Ecospace up to the point where intended simulations begin
            var tcs = new TaskCompletionSource();
            void Handler(RunStates state)
            {
                if (state == RunStates.waiting)
                    tcs.TrySetResult();
            }
            OnRunStateChanged += Handler;

            // Phew, we managed to plow through. Run Ecospace!
            this.m_thread = new Thread(RunEcospace);
            this.m_thread.Start();

            // Set spin-up progress trackers. Needed because we need to look one time step ahead for pausing
            this.m_nSpinUpSteps = ds.UseSpinUp ? (int)(ds.SpinUpYears / ds.TimeStep) : 0;
            this.m_iSpinUpStep = 0;

            var completedTask = await Task.WhenAny(tcs.Task); //, Task.Delay(timeoutMs ));
            OnRunStateChanged -= Handler;

            if (completedTask != tcs.Task)
            {
                // We hit a timeout; need to log that
                this.m_logger.LogInformation("EwE - Ecospace initialization timed out; this run is dead in the water");
                this.ForceStop();
            }
            // Ready when running Ecospace is waiting for further instructions
            return (RunState == RunStates.waiting) ? 1 : -1;
        }

        /// <summary>
        /// Asynchronousely execute a month
        /// </summary>
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
            this.m_logger.LogInformation("EwE - Continue");
            this.RunState = RunStates.running;
            this.m_core.EcospacePaused = false;

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            this.OnRunStateChanged -= Handler;

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
                    tcs.TrySetResult();
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
                return true; // All good

            // Timeout hit: force kill
            this.OnRunStateChanged -= Handler;
            this.ForceStop();
            return false;
        }

        public Task<bool> UpdatePricesAsync(List<SpeciesPrice> speciesPrices)
        {
            // Make prices up for grabs
            this.m_pricesIn = speciesPrices;
            return Task.FromResult(true);
        }

        public Task<bool> UpdateCatchDispositionSummaryAsync(CatchDispositionSummary catchDispositionSummary)
        {
            // Make catch dispositions up for grabs
            this.m_catchIn = catchDispositionSummary;
            return Task.FromResult(true);
        }

        public Task<Biomass> GetBiomassAsync()
        {
            // Return a dummy if not available
            if (this.m_biomassOut == null)
                this.m_biomassOut = new Biomass() { MeasurementUnit = "kg" };

            // ToDo: need to clear out biomass once dispatched?
            return Task.FromResult(this.m_biomassOut);
        }

        public Task<List<SalesSummary>> GetSalesSummariesAsync(DateTime start, DateTime end)
        {
            // ToDo: need to clear out sales once dispatched?
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
                int iFleet = m_configuration.ResolveMarket(price.GearCode, price.MarketCode).match?.Index ?? 0;
                if (iFleet > 0)
                {
                    foreach (var info in m_configuration.ResolveGroups(price.SpeciesCode))
                    {
                        float pr = (float)price.Price;
                        EwEMapping key = info.match;

                        // ToDo: implement unit conversions?
                        //Debug.Assert(string.Compare(price.Currency, "eur", true) == 0);
                        //Debug.Assert(string.Compare(price.MeasurementUnit, "kg", true) == 0);

                        if (iFleet > 0 && key.Index > 0)
                            ds.Market[iFleet, key.Index] = (float)price.Price;
                        else
                        {
                            // ToDo_JS: decide how to respond to a potential EwE misconfiguration.
                            this.m_logger.LogWarning("EwE - !! Price record gear '{0}', market '{1}', species '{2}' cannot be mapped to EwE", price.GearCode, price.MarketCode, price.SpeciesCode);
                            //throw new Exception("Price record gear '{0}', market '{1}', species '{2}' cannot be mapped to EwE", price.GearCode, price.marketCode, price.SpeciesCode);
                        }
                    }
                }
            }

            // Done, clear buffer. Prices within EwE will remain fixed until the next change
            this.m_pricesIn = null;
        }

        private void IntegrateCatchDispositions(int iTime)
        {
            if (this.m_catchIn == null) return;
            if (this.m_configuration == null) return;

            var ds = this.m_core.EcospaceDataStructures;
            var bm = this.m_core.EcospaceBasemap;

            foreach (var grid in this.m_catchIn.DispositionGrids)
            {
                // Try to parse species code in grid
                MultiLevelKey key = MultiLevelKey.FromObject(grid.Species);
                // Resolve mapping key for grid fleet segment
                EwEMapping keyFkeet = m_configuration.ResolveFleet(grid.FleetSegment).match;

                foreach (var groupinfo in m_configuration.ResolveGroups(key))
                {
                    EwEMapping keyGroup = groupinfo.match;

                    int iGroup = keyGroup.Index;
                    int iFleet = keyFkeet.Index;

                    // ToDo: validate group and fleet codes

                    foreach (var cell in grid.DispositionCells)
                    {
                        int ir = (int)Math.Floor(bm.LatToRow((float)cell.Latitude));
                        int ic = (int)Math.Floor(bm.LonToCol((float)cell.Longitude));

                        if (1 <= ir & ir <= ds.InRow & 1 <= ic & ic <= ds.InCol)
                            if (ds.Depth[ir, ic] > 0)
                            {
                                // Stop this Ecospace fleet from fishing in this cell - should in fact not fish anywhere anymore for this time step!
                                ds.EffortSpace[iFleet, ir, ic] = 0;
                                ds.PAreaFished[iFleet][ir, ic] = 0;

                                float @catch = KgToDensity(cell.GrossCatchBiomass - cell.LiveDiscardsBiomass, ir, ic);
                                float deaddisc = KgToDensity(cell.DeadDiscardsBiomass, ir, ic);
                                float available = ds.Bcell[ir, ic, iGroup];

                                Debug.Assert(@catch >= 0, "Cannot fish negatively. Would be nice, but sorry, no.");
                                Debug.Assert(ds.Depth[ir, ic] > 0, "Not a modelled cell?!");

                                if (@catch > available)
                                {
                                    // WHoah!! External fishing is catching more than is available in this cell
                                    throw new Exception(string.Format("EwE controller cannot integrate Catch Disposition {0} kg ({1} t/km2), into cell {2}x{3} ({4}x{5}), only {6} t/km2 available in Ecospace",
                                        (cell.GrossCatchBiomass - cell.LiveDiscardsBiomass), @catch, cell.Longitude, cell.Latitude, ic, ir, available));
                                }

                                // Leave some tiny biomass in the cell; fisheries cannot catch it all (and Ecospace does not like divisions by zero)
                                @catch = (float)Math.Max(1E-10, available - @catch);

                                m_groupSpeciesProportions[iGroup].ApplyFishingMortality(ir, ic, keyGroup, @catch, (double)ds.Bcell[ir, ic, iGroup]);

                                ds.Bcell[ir, ic, iGroup] = @catch;
                                ds.CatchMap[ir, ic, iGroup] += @catch;
                                ds.CatchFleetMap[ir, ic, iFleet] += @catch;
                                ds.Landings[iFleet, iGroup] += @catch;
                                ds.DiscardsMap[ir, ic, iFleet] += deaddisc;
                                ds.ResultsByFleetGroup[(int)eSpaceResultsFleetsGroups.CatchBio, iFleet, iGroup, iTime] += @catch;
                            }
                    }
                }
            }
            // Catches have been processed
            this.m_catchIn = null;
            // Recover and normalize species proportions
            foreach (var prop in m_groupSpeciesProportions.Values)
            {
                prop.ApplyRecovery();
                prop.NormalizeDirtyCells();
            }
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

                foreach (EwEMapping key in this.m_configuration.Mappings(KeyDomain.Species))
                {
                    Species? species = key.ToObject<Ecopath.Models.Species>();
                    if (species != null)
                    {
                        BiomassGrid grid = new BiomassGrid()
                        {
                            Species = species
                        };
                        int iGroup = key.Index;

                        for (int ic = 1; ic <= ds.InCol; ic++)
                            for (int ir = 1; ir <= ds.InRow; ir++)
                                if (ds.Depth[ir, ic] > 0)
                                {
                                    // Express biomass of group proportion in kg at timestep units (not annual)
                                    double biomassCell = DensityToKg(ds.Bcell[ir, ic, iGroup], ir, ic) * ds.TimeStep;
                                    double biomassSpecies = m_groupSpeciesProportions[iGroup].GetSpeciesBiomass(ir, ic, key, biomassCell);

                                    grid.BiomassCells.Add(new BiomassCell()
                                    {
                                        Latitude = bm.RowToLat(ir),
                                        Longitude = bm.ColToLon(ic),
                                        Biomass = biomassSpecies
                                    });
                                }
                        this.m_biomassOut.BiomassGrids.Add(grid);
                    }
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

            cEcopathDataStructures ecopathds = this.m_core.EcopathDataStructures;
            cEcospaceDataStructures spaceds = this.m_core.EcospaceDataStructures;
            cEcospaceBasemap bm = this.m_core.EcospaceBasemap;

            // Tally absolute sales over all catch dispositions
            Dictionary<(int Group, int Fleet), (double Volume, double Value)> TotalSales = new();
            HashSet<int> markets = new();

            foreach (int iGroup in this.m_configuration.FishedGroups)
            {
                EwEMapping? mlkGroup = this.m_configuration.Find(iGroup, KeyDomain.Species);
                float sppProp = mlkGroup?.Proportion ?? 0;

                for (int iFleet = 1; iFleet <= this.m_core.nFleets; iFleet++)
                {
                    // Only report fleets fished by EwE
                    if (!this.m_configuration.IsExternalFleet(iFleet))
                    {
                        // Tally up the catch dispositions for all the markets this gear code caters to
                        MultiLevelKey? mlkFleet = this.m_configuration.Find(iFleet, KeyDomain.FleetSegment);
                        MultiLevelKey? mlkMarket = this.m_configuration.Find(iFleet, KeyDomain.Market);

                        string market = mlkMarket?.GetField("marketcode") ?? string.Empty;

                        double[,] catches = new double[spaceds.InRow + 1, spaceds.InCol + 1];
                        double[,] deaddisc = new double[spaceds.InRow + 1, spaceds.InCol + 1];
                        double[,] livedisc = new double[spaceds.InRow + 1, spaceds.InCol + 1];
                        bool bHasData = false;

                        if (ecopathds.Landing[iFleet, iGroup] + ecopathds.Discard[iFleet, iGroup] > 0)
                            for (int ir = 1; ir <= spaceds.InRow; ir++)
                                for (int ic = 1; ic <= spaceds.InCol; ic++)
                                    if (spaceds.Depth[ir, ic] > 0)
                                    {
                                        // Convert EwE annual densities to monthly absolutes
                                        double cellCatchesAbs = DensityToKg(spaceds.CatchGroupFleetMap[iFleet, iGroup][ir, ic], ir, ic) * sppProp * spaceds.TimeStep;
                                        double cellLiveDiscAbs = DensityToKg(spaceds.DiscardSurviveGroupFleetMap[iFleet, iGroup][ir, ic], ir, ic) * sppProp * spaceds.TimeStep;
                                        double cellDeadDiscAbs = DensityToKg(spaceds.DiscardMortGroupFleetMap[iFleet, iGroup][ir, ic], ir, ic) * sppProp * spaceds.TimeStep;

                                        // Tally sales. I'm sure this can be done more elegantly but hey
                                        (int Group, int Fleet) salekey = new();
                                        (double Volume, double Value) saleTot = new(0, 0);
                                        if (TotalSales.ContainsKey(salekey))
                                            saleTot = TotalSales[salekey];
                                        else
                                            TotalSales[salekey] = saleTot;
                                        saleTot.Volume += (cellCatchesAbs - cellDeadDiscAbs);
                                        saleTot.Value += (cellCatchesAbs - cellDeadDiscAbs) * ecopathds.Market[iFleet, iGroup];

                                        // Prepare catch deposition. Note that EwE catches do NOT include live discards
                                        // ToDo_JS: Decide on the below. What is the framework expecting? 
                                        // cellCatchesAbs += cellLiveDiscAbs;

                                        catches[ir, ic] += cellCatchesAbs;
                                        livedisc[ir, ic] += cellLiveDiscAbs;
                                        deaddisc[ir, ic] += cellDeadDiscAbs;
                                        bHasData = true;
                                    }

                        // Finally prepare data for the framework
                        if (bHasData)
                        {
                            // Prepare disposition grid
                            var grid = new DispositionGrid()
                            {
                                FleetSegment = mlkMarket?.ToObject<Ecopath.Models.FleetSegment>(),
                                Species = mlkGroup?.ToObject<Ecopath.Models.Species>()
                            };
                            for (int ir = 1; ir <= spaceds.InRow; ir++)
                                for (int ic = 1; ic <= spaceds.InCol; ic++)
                                    if (spaceds.Depth[ir, ic] > 0)
                                    {
                                        grid.DispositionCells.Add(new DispositionCell()
                                        {
                                            Latitude = bm.RowToLat(ir),
                                            Longitude = bm.ColToLon(ic),

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
            for (int iFleet = 1; iFleet <= this.m_core.nFleets; iFleet++)
            {
                // Only report fleets fished by EwE
                if (!this.m_configuration.IsExternalFleet(iFleet))
                {
                    MultiLevelKey? mlkFleet = this.m_configuration.Find(iFleet, KeyDomain.FleetSegment);
                    MultiLevelKey? mlkMarket = this.m_configuration.Find(iFleet, KeyDomain.Market);
                    var sales = new SalesSummary()
                    {
                        MarketId = mlkMarket?.GetField("marketcode") ?? string.Empty,
                        MeasurementUnit = "kg",
                        Currency = "EUR", // No conversion here
                        Sales = new List<Sale>()
                    };
                    foreach ((int Group, int Fleet) saleKey in TotalSales.Keys.Where(k => k.Fleet == iFleet))
                    {
                        MultiLevelKey? mlkSpecies = this.m_configuration.Find(saleKey.Group, KeyDomain.Species);
                        if (TotalSales.TryGetValue(saleKey, out var saleTot))
                        {
                            Sale s = new Sale()
                            {
                                GearCode = mlkFleet?.GetField("gearcode") ?? string.Empty,
                                SpeciesCode = mlkSpecies?.GetField("speciescde") ?? string.Empty,
                                Quantity = saleTot.Volume,
                                Value = saleTot.Value
                            };
                            sales.Sales.Add(s);
                        }
                    }
                    this.m_salesOut.Add(sales);
                }

                //// Sanity check
                //if (salesValue.Keys.Count() > 0)
                //{
                //    throw new Exception("There are {0} unexpected sales record(s). Please kick the EwE developers.");
                //}
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

        private void ForceStop()
        {
            this.m_logger.LogInformation("EwE - !! Force stop received");
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

        /// -------------------------------------------------------------------
        /// <summary>
        /// Plug-in callback that triggers all logic to extract data from, and 
        /// inject data into, the running Ecospace model.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="iTime"></param>
        /// -------------------------------------------------------------------
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

                    case cEcospaceBridgePlugin.EventType.BeginRun:
                        break;

                    case cEcospaceBridgePlugin.EventType.BeginTimeStep:
                        // Prices need to be integerated into the start of a time step for EwE effort distributions
                        this.IntegratePrices();

                        cEcospaceDataStructures ds = this.m_core.EcospaceDataStructures;
                        if (ds.bInSpinUp)
                        {
                            // Tick
                            this.m_iSpinUpStep += 1;
                            if (this.m_iSpinUpStep % cCore.N_MONTHS == 0)
                                this.m_logger.LogInformation("EwE - finished spinup year {0}", (int) (this.m_iSpinUpStep / cCore.N_MONTHS));
                        }
                        else
                        {
                            if (iTime % cCore.N_MONTHS == 0)
                                this.m_logger.LogInformation("EwE - finished year {0}", this.m_core.EcospaceTimestepToAbsoluteTime(iTime).Year);
                        }
                        break;

                    case cEcospaceBridgePlugin.EventType.BeginTimeStepPost:
                        // NOP
                        break;

                    case cEcospaceBridgePlugin.EventType.EndTimeStep:

                        // Do we need to pause at the start of the next time step?
                        if (this.MustPauseNext(iTime + 1))
                        {
                            // Prepare data for sending out
                            this.CacheBiomassData();
                            this.CacheCatchAndSalesData();

                            this.m_logger.LogInformation("EwE - pausing at timestep {0}", iTime + 1);
                            this.RunState = RunStates.waiting;
                            this.m_core.EcospacePaused = true;
                        }
                        break;

                    case cEcospaceBridgePlugin.EventType.EffortDistrPost:
                        // Effort has been distributed, and Ecospace is about to calculate ecosystem dynamics and fishing
                        // This is the perfect moment to integrate any remotely calculated catch dispositions into the running core
                        this.IntegrateCatchDispositions(iTime);
                        break;

                    case cEcospaceBridgePlugin.EventType.EndRun:
                        this.m_logger.LogInformation("EwE - end run callback");
                        
                        // Clear all modifications made to core data, if any
                        this.m_core.DiscardChanges();
                        // Correctly reset the state and clean up
                        this.RunState = RunStates.idle;
                        this.m_thread = null;
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

        private bool MustPauseNext(int iTime)
        {
            // Do not halt while in spin-up
            cEcospaceDataStructures ds = m_core.EcospaceDataStructures;
            if (m_iSpinUpStep + 1 < m_nSpinUpSteps) return false;
            DateTime dt = this.m_core.EcospaceTimestepToAbsoluteTime(iTime);
            return (dt.Year >= m_configuration?.StartYear);
        }

        #endregion // Internal - EwE interactions

    }
}
