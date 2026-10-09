using Ecopath.EwE.Prices;
using Ecopath.EwE.Wrapper;
using Ecopath.Services;
using Eii.BlobStore;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using EwEBridge.Ecospace;
using EwECore;
using EwECore.Common;
using EwECore.Plugins;
using EwECore.SpatialData;
using EwEUtils.Utilities;
using SURIMI.Datamodel;
using System.Diagnostics;
using System.Text;

namespace Ecopath.EwE
{
    // About the flow of Ecospace and this controller:
    // - The aim was to insert agent-based fisheries into EwE with a minimal code changes
    // - EwE needs to account for this fishing in the running model data, but also in the various result arrays
    //
    // This implementation relies on a plug-in _priceBridge (to supercharge EwE interop) and the 'off-the-shelf' Ecospace pause mechanism
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
    // ! devise a mechanism to _priceBridge time step sizes; right now the code assumes that time steps are monthly
    // ! expand entity matching logic
    // ! devise system to order up the same scenario across all participating models
    // ! user stories in GitHub!!!

    /// =======================================================================
    /// <summary>
    /// Shell class that provides the interactions between SURIMI and the EwE model.
    /// </summary>
    /// =======================================================================
    public class EwEController : IEwEController
    {
        #region Private vars 

        /// <summary>The <see cref="cCore"/> to operate on.</summary>
        private readonly IEwECore m_core;
        /// <summary>The Ecospace run thread, if any.</summary>
        private Thread? _thread;
        /// <summary>EwE configuration that defines how EwE entities relate to common concepts (species, fishing, markets, etc).</summary>
        private IEwEConfiguration? _configuration;
        private readonly IBlobStore _blobStore;

        private readonly IEwEConfigurationService _configurationService;
        private readonly ILogger<EwEController> _logger;
        private readonly IKeyFieldDescriptorRegistry _keyFieldDescriptorRegistry;
        private readonly IMultiLevelKeyFactory _multiLevelKeyFactory;

        private RunStates m_runstate = RunStates.idle;

        /// <summary>Event for internal state monitoring.</summary>
        private event Action<RunStates>? OnRunStateChanged;

        // --- Data in and out 
        private List<SpeciesPrice>? _pricesIn;
        private CatchDispositionSummary? _catchIn;

        private Biomass? _biomassOut;
        private CatchDispositionSummary? _catchOut;
        private List<SalesSummary> _salesOut = new();

        // --- Internal tracking
        private int _numSpinUpSteps = 0;
        private int _spinUpStep = 0;

        private double _minimumSaleQuantity = 1.0; // Minimum sale quantity in kg to be reported to the market. 

        /// <summary>
        /// To track species group proportions affected by external fishing
        /// </summary>
        private Dictionary<int, GroupSpeciesProportions> _groupSpeciesProportions = new();

        private PriceBridge? _priceBridge = null;

        #endregion // Private vars 

        public EwEController(ILogger<EwEController> logger, IEwEConfigurationService configurationService, IEwECore core, IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry, IMultiLevelKeyFactory multiLevelKeyFactory, IVocabulariesRegisterService vocabulariesRegisterService, IBlobStore lobStore)
        {
            m_core = core;
            _keyFieldDescriptorRegistry = keyFieldDescriptorRegistry;
            _multiLevelKeyFactory = multiLevelKeyFactory;
            _configurationService = configurationService;
            vocabulariesRegisterService.RegisterVocabularies();

            RunState = RunStates.idle;

            // To make sure we can find local resources. This is rather hack.
            Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
            _logger = logger;
            _blobStore = lobStore;
        }

        ~EwEController()
        {
            ForceStop();
            m_core.Teardown(); // No-op if already torn down
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
            get => m_runstate;
            private set
            {
                if (m_runstate != value)
                {
                    //Console.WriteLine("Run state set to " + value.ToString());
                    m_runstate = value;
                    OnRunStateChanged?.Invoke(m_runstate);
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
        public async Task<int> StartAsync(SurimiContract surimiContract, string scenarioName, string climateScenarioCode, CancellationToken cancellationToken)
        {
            // Check readiness
            if (RunState != RunStates.idle)
                throw new Exception("EwE controller already busy, aborting");

            // Commence configuration
            RunState = RunStates.starting;

            // Create the EwE core and wire the bridge plugin for this simulation
            m_core.Initialize();
            _logger.LogInformation("EwE loaded {NrOfPlugins} plug-in(s)", m_core.PluginManager.LoadPlugins());

            IPlugin? pi = GetPlugin(typeof(cEcospaceBridgePlugin));
            if (pi != null)
            {
                cEcospaceBridgePlugin ppt = (cEcospaceBridgePlugin)pi;
                ppt.BridgeCallback = BridgeCallback;
            }

            _configuration = await _configurationService.CreateConfigurationAsync(scenarioName, cancellationToken);

            // if on S3, the entire "scenarioName" directory is now downloaded to the local instance, including the ModelFile
            if (!m_core.LoadModel(_configuration.LocalModelFile))
                throw new Exception($"EwE could not load model '{_configuration.LocalModelFile}'");
            _logger.LogInformation("EwE - Ecopath loaded file '{LocalModelFile}', model '{ModelName}'", _configuration.LocalModelFile, m_core.EcopathDataStructures.ModelName);

            if (!string.IsNullOrEmpty(climateScenarioCode))
            {
                var externalDatasetsFileName = @$"{scenarioName}_drivers_{climateScenarioCode.Replace(".", "").ToLower()}_annual.xml";
                if (await _blobStore.ExistsAsync(externalDatasetsFileName, PathType.Input))
                {
                    var externalDatasetsLocalFileName = Path.Combine(_blobStore.LocalInputRoot, externalDatasetsFileName);

                    cSpatialDataConnectionManager man = m_core.SpatialDataConnectionManager;
                    cSpatialDataSetManager dsm = man.DatasetManager();
                    if (!dsm.Load(externalDatasetsLocalFileName, true))
                    {
                        RunState = RunStates.idle;
                        throw new Exception("EwE - Could not load STDF data from '" + externalDatasetsLocalFileName + "'");
                    }

                    _logger.LogInformation("Loaded STDF data from '{ConfigFile}', {DatasetCount} dataset(s)", externalDatasetsLocalFileName, dsm.Datasets().Length);

                    foreach (ISpatialDataSet dset in dsm.Datasets())
                    {
                        string sType = cTypeUtils.TypeToString(dset.GetType());
                        string sName = dset.CustomName;

                        // Can't access inaccessible cSpatialDataSetPlaceholder type, so check for "placeholder" in the type name
                        if (sType.ToLower().Contains("placeholder"))
                        {
                            RunState = RunStates.idle;
                            throw new Exception("EwE - Could not resolve STDF dataset of type '" + sType + "'");
                        }

                        _logger.LogInformation("- {DatasetName} ({DatasetType})", sName, sType);
                    }
                }
                else
                {
                    // It's fine if no climate data has been found, but good to log that
                    _logger.LogInformation("EwE - No climate data found");
                }
            }
            else
            {
                // It's fine if no climate is provided, but good to log that
                _logger.LogInformation("EwE - No climate scenario specified");
            }

            // Check Ecopath balancing
            bool bIsBalanced = false;
            if (!m_core.RunEcopath(ref bIsBalanced) | !bIsBalanced)
            {
                RunState = RunStates.idle;
                throw new Exception("EwE - Ecopath does not balance");
            }

            // Key admin bit
            for (int iGroup = 1; iGroup <= m_core.nGroups; iGroup++)
                if (m_core.get_EcopathGroupInputs(iGroup).IsFished)
                    _configuration.FishedGroups.Add(iGroup);

            _logger.LogInformation("EwE - Ecopath does balance");

            // Check and load Ecosim
            if (_configuration.EcosimScenario <= 0 | !m_core.LoadEcosimScenario(_configuration.EcosimScenario))
            {
                RunState = RunStates.idle;
                throw new Exception($"EwE - Ecosim scenario {_configuration.EcosimScenario} not loaded");
            }
            _logger.LogInformation("EwE - Ecosim scenario {EcosimScenario} loaded", _configuration.EcosimScenario);
            if (_configuration.EcosimTimeSeries > 0)
            {
                if (!m_core.LoadTimeSeries(_configuration.EcosimTimeSeries))
                {
                    RunState = RunStates.idle;
                    throw new Exception($"EwE - Ecosim time series {_configuration.EcosimTimeSeries} not loaded");
                }
                _logger.LogInformation("EwE - Ecosim time series {EcosimTimeSeries} loaded", _configuration.EcosimTimeSeries);
            }

            // Configure Ecosim
            cEcoSimModelParameters parmsSim = m_core.EcosimModelParameters;
            parmsSim.NumberYears = _configuration.MaxRunYears; // No of years apply to both Sim and Space

            // Run Ecosim
            _logger.LogInformation("EwE - Going to run Ecosim for {NumberYears} years", parmsSim.NumberYears);
            if (!m_core.RunEcosim())
            {
                RunState = RunStates.idle;
                throw new Exception("EwE - Ecosim failed to run");
            }
            _logger.LogInformation("EwE - Ecosim run successfully");

            // Check and load Ecospace
            if (_configuration.EcospaceScenario <= 0 | !m_core.LoadEcospaceScenario(_configuration.EcospaceScenario))
            {
                RunState = RunStates.idle;
                throw new Exception($"EwE - Ecospace scenario {_configuration.EcospaceScenario} not loaded");
            }
            _logger.LogInformation("EwE - Ecospace scenario {EcospaceScenario} loaded", _configuration.EcospaceScenario);

            // Now load the configuration
            try
            {
                await _configurationService.LoadAsync(m_core, _configuration, surimiContract);
            }
            catch (Exception ex)
            {
                RunState = RunStates.idle;
                throw new Exception("EwE - Semantic configuration failed to load: " + ex.Message);
            }

            StringBuilder info = new();
            info.AppendLine("EwE FG - species mappings:");
            foreach (var mapping in _configurationService.Mappings(KeyDomain.Species))
                info.AppendLine(string.Format(" - {0}", GetMappingInfoString(mapping, m_core)));
            info.AppendLine("EwE fleet - fleetsegment mappings:");
            foreach (var mapping in _configurationService.Mappings(KeyDomain.FleetSegment))
                info.AppendLine(string.Format(" - {0}", GetMappingInfoString(mapping, m_core)));
            info.AppendLine("EwE fleet - market mappings:");
            foreach (var mapping in _configurationService.Mappings(KeyDomain.Market))
                info.AppendLine(string.Format(" - {0}", GetMappingInfoString(mapping, m_core)));
            _logger.LogInformation("{MappingInfo}", info.ToString());

            // Calculate base prices
            CalculateBasePrices(true);
            // Build species proportion accounting
            foreach (int iGroup in _configuration.FishedGroups)
                _groupSpeciesProportions[iGroup] = GroupSpeciesProportionsFactory.Create(m_core, iGroup, _configurationService.Mappings(KeyDomain.Species));

            // Configure Ecospace
            cEcospaceDataStructures ds = m_core.EcospaceDataStructures;
            cEcospaceModelParameters parmsSpace = m_core.EcospaceModelParameters;

            ds.SpinUpYears = _configuration.SpinupYears;
            ds.UseSpinUp = (_configuration.SpinupYears > 0);
            _logger.LogInformation("EwE - Ecospace spin-up for {SpinupYears} years", ds.UseSpinUp ? _configuration.SpinupYears.ToString() : "off");

            // Configure output writers
            string outputPath = _configuration.OutputPath;
            // This propagates to all writers when they need it. Set on cCore
            m_core.OutputPath = outputPath;
            // The first time step to write output to is Ecospace-only. And why? No idea, but that's how EwE rolls
            parmsSpace.FirstOutputTimeStep = m_core.AbsoluteTimeToEcospaceTimestep(new DateTime(_configuration.StartYear, 1, 1));
            // Filtering for monthy/annual output is also Ecospace-only
            parmsSpace.UseAnnualOuput = false; // We want all time steps
            // Now enable the right writers
            for (int i = 0; i < parmsSpace.nResultWriters - 1; i++)
            {
                // Lovely one-based indexing in EwE
                IEcospaceResultsWriter writer = parmsSpace.ResultWriter(i + 1);
                // Some decision to be made here about what writers to enable
                bool bEnable = (writer is cEcospaceASCMapBiomassWriter) || (writer is cEcospaceASCMapCatchWriter) || (writer is cEcospaceRegionAvgResultsWriter);
                // There you go
                writer.Enabled = bEnable && _configuration.WriteOutput;
            }

            // Start running Ecospace up to the point where intended simulations begin
            var tcs = new TaskCompletionSource();
            void Handler(RunStates state)
            {
                if (state == RunStates.waiting)
                    tcs.TrySetResult();
            }
            OnRunStateChanged += Handler;

            // Phew, we managed to plow through. Run Ecospace!
            _thread = new Thread(RunEcospace);
            _thread.Start();

            // Set spin-up progress trackers. Needed because we need to look one time step ahead for pausing
            _numSpinUpSteps = ds.UseSpinUp ? (int)(ds.SpinUpYears / ds.TimeStep) : 0;
            _spinUpStep = 0;

            var completedTask = await Task.WhenAny(tcs.Task);
            OnRunStateChanged -= Handler;

            if (completedTask != tcs.Task)
            {
                // We hit a timeout; need to log that
                _logger.LogInformation("EwE - Ecospace initialization timed out; this run is dead in the water");
                ForceStop();
            }
            // Ready when running Ecospace is waiting for further instructions
            return (RunState == RunStates.waiting) ? 1 : -1;
        }

        /// <summary>
        /// Asynchronousely execute a month
        /// </summary>
        public async Task<bool> ContinueAsync(int timeoutMs = 60000)
        {
            if (RunState != RunStates.waiting) return false;

            // Need to wait for RunState to switch back to Waiting. Only return after
            var tcs = new TaskCompletionSource();

            void Handler(RunStates state)
            {
                if (state == RunStates.waiting)
                    tcs.TrySetResult();
            }
            OnRunStateChanged += Handler;

            // Carry on
            _logger.LogInformation("EwE - Continue");
            RunState = RunStates.running;
            m_core.EcospacePaused = false;

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            OnRunStateChanged -= Handler;

            return true;
        }

        /// <summary>
        /// Stop any simulation. Interrupts the Ecospace thread and tears down the EwE core.
        /// Never calls StopEcospace() because that call does not return.
        /// </summary>
        /// <returns></returns>
        public Task<bool> StopAsync(int timeoutMs = 10000)
        {
            _logger.LogInformation("EwE - stopping simulation");

            ForceStop(); // Interrupt the Ecospace thread and set RunState = idle

            // Give the thread a brief window to observe the interrupt and exit cleanly
            _thread?.Join(2000);
            _thread = null;

            m_core.Teardown(); // CloseModel + Dispose + null internals
            _configuration = null;

            return Task.FromResult(true);
        }

        public Task<bool> UpdatePricesAsync(List<SpeciesPrice> speciesPrices)
        {
            // Make prices up for grabs
            _pricesIn = speciesPrices;
            return Task.FromResult(true);
        }

        public Task<bool> UpdateCatchDispositionSummaryAsync(CatchDispositionSummary catchDispositionSummary)
        {
            // Make catch dispositions up for grabs
            _catchIn = catchDispositionSummary;
            return Task.FromResult(true);
        }

        public Task<Biomass> GetBiomassAsync()
        {
            // Return a dummy if not available
            if (_biomassOut == null)
                _biomassOut = new Biomass() { MeasurementUnit = "kg" };

            // ToDo: need to clear out biomass once dispatched?
            return Task.FromResult(_biomassOut);
        }

        public Task<List<SalesSummary>> GetSalesSummariesAsync(DateTime start, DateTime end)
        {
            // ToDo: need to clear out sales once dispatched?
            return Task.FromResult(_salesOut);
        }

        public Task<CatchDispositionSummary> GetCatchDispositionSummaryAsync(DateTime start, DateTime end)
        {
            if (_catchOut == null)
                _catchOut = new CatchDispositionSummary();
            return Task.FromResult(_catchOut);
        }

        #endregion // Public interaction

        #region Data interactions

        private string GetMappingInfoString(EwEMapping mapping, IEwECore core)
        {
            cCoreInputOutputBase? item = null;

            switch (mapping.Domain)
            {
                case KeyDomain.Species:
                    item = core.get_EcopathGroupInputs(mapping.Index);
                    break;
                case KeyDomain.FleetSegment:
                case KeyDomain.Market:
                    item = core.get_EcopathFleetInputs(mapping.Index);
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
            if (item == null)
                return string.Format("INVALID {0} => {1}", mapping.Index, mapping.ToString());

            return string.Format("EwE index {0}:\"{1}\" @{2} => {3}", mapping.Index, item.Name, mapping.Proportion, mapping.ToString());
        }

        /// <summary>
        /// This method integrates the prices received from the Market model into the EwE model. It maps to a Market price per fleet and group.
        /// </summary>
        /// <remarks>
        /// 03 July 2026: prices are now by species; the gear code no longer applies
        //  What these changes call for:
        // - Each fleet has a market code attached
        // - Prices are set by species + market, applying to all the fleets that fall within the same market
        // - Prices are applied proportionally to the EwE off-vessel prices, PER MARKET
        /// </remarks>
        private void IntegratePrices()
        {
            if (_pricesIn == null) return;
            if (_configuration == null) return;
            if (_priceBridge == null) return;

            foreach (var price in _pricesIn)
            {
                float pr = (float)price.Price;

                foreach (var marketinfo in _configurationService.ResolveEwEFleet(price.MarketCode))
                {
                    int iFleet = marketinfo.EwEMapping.Index;
                    foreach (var groupinfo in _configurationService.ResolveEwEGroupFromSpecies(price.SpeciesCode))
                    {
                        int iGroup = groupinfo.EwEMapping.Index;

                        _priceBridge.AddEvolvingPrice(price.MarketCode, iGroup, pr, iFleet, 1.0);
                    }
                }
            }

            // Done, clear buffer. Prices within EwE will remain fixed until the next change
            _priceBridge.AppyToEwE();

            _pricesIn = null;
        }

        private void IntegrateCatchDispositions(int iTime)
        {
            if (_catchIn == null) return;
            if (_configuration == null) return;

            var ds = m_core.EcospaceDataStructures;
            var bm = m_core.EcospaceBasemap;

            foreach (var grid in _catchIn.DispositionGrids)
            {
                // Try to parse species code in grid
                MultiLevelKey key = _multiLevelKeyFactory.FromObject(grid.Species, KeyDomain.Species, _keyFieldDescriptorRegistry);

                if (grid.FleetSegment == null)
                    throw new Exception(string.Format("EwE controller cannot integrate Catch Disposition for species {0} because the fleet segment is missing", key.ToString()));

                var fleetCode = grid.FleetSegment?.ToString() ?? "";
                // Resolve mapping key for grid fleet segment. This ONLY works because the fleet design aligns 100% with the gear+market design
                foreach (var fleetinfo in _configurationService.ResolveEwEFleet(fleetCode))
                {
                    int iFleet = fleetinfo.EwEMapping.Index;
                    foreach (var groupinfo in _configurationService.ResolveEwEGroup(key))
                    {
                        int iGroup = groupinfo.EwEMapping.Index;
                        // Validate group and fleet codes
                        if (iGroup > 0 && iFleet > 0)
                        {
                            // Apply a proportion of the selected group is a multi-stanza group that the catch disposition did not identify as such
                            float scalar = this.StanzaWideModifier(iGroup, key);

                            foreach (var cell in grid.DispositionCells)
                            {
                                int ir = (int)Math.Floor(bm.LatToRow((float)cell.Latitude));
                                int ic = (int)Math.Floor(bm.LonToCol((float)cell.Longitude));

                                if (1 <= ir && ir <= ds.InRow && 1 <= ic && ic <= ds.InCol)
                                {
                                    if (ds.Depth[ir, ic] > 0)
                                    {
                                        // Stop this Ecospace fleet from fishing in this cell - should in fact not fish anywhere anymore for this time step!
                                        ds.EffortSpace[iFleet, ir, ic] = 0;
                                        ds.PAreaFished[iFleet][ir, ic] = 0;

                                        float catchAmount = KgToDensity(cell.GrossCatchBiomass - cell.LiveDiscardsBiomass, ir, ic) * scalar;
                                        float deaddisc = KgToDensity(cell.DeadDiscardsBiomass, ir, ic) * scalar;
                                        float available = ds.Bcell[ir, ic, iGroup];

                                        Debug.Assert(catchAmount >= 0, "Cannot fish negatively. Would be nice, but sorry, no.");
                                        Debug.Assert(ds.Depth[ir, ic] > 0, "Not a modelled cell?!");

                                        if (catchAmount > available)
                                        {
                                            // WHoah!! External fishing is catching more than is available in this cell
                                            throw new Exception(string.Format("EwE controller cannot integrate Catch Disposition {0} kg ({1} t/km2), into cell {2}x{3} ({4}x{5}), only {6} t/km2 available in Ecospace",
                                                (cell.GrossCatchBiomass - cell.LiveDiscardsBiomass), catchAmount, cell.Longitude, cell.Latitude, ic, ir, available));
                                        }

                                        // Leave some tiny biomass in the cell; fisheries cannot catch it all (and Ecospace does not like divisions by zero)
                                        float remaining = (float)Math.Max(1E-10f, available - catchAmount);
                                        catchAmount = available - remaining;    // clamp against 1E-10 floor

                                        _groupSpeciesProportions[iGroup].ApplyFishingMortality(ir, ic, groupinfo.EwEMapping, catchAmount, (double)ds.Bcell[ir, ic, iGroup]);

                                        ds.Bcell[ir, ic, iGroup] = remaining;

                                        ds.CatchMap[ir, ic, iGroup] += catchAmount;
                                        ds.CatchFleetMap[ir, ic, iFleet] += catchAmount;
                                        ds.Landings[iFleet, iGroup] += catchAmount;
                                        ds.DiscardsMap[ir, ic, iFleet] += deaddisc;
                                        ds.ResultsByFleetGroup[(int)eSpaceResultsFleetsGroups.CatchBio, iFleet, iGroup, iTime] += catchAmount;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Catches have been processed
            _catchIn = null;
            // Recover and normalize species proportions
            foreach (var prop in _groupSpeciesProportions.Values)
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
            _biomassOut = new Biomass() { MeasurementUnit = "kg" };
            if (_configuration != null)
            {
                cEcospaceDataStructures ds = m_core.EcospaceDataStructures;
                cEcospaceBasemap bm = m_core.EcospaceBasemap;

                foreach (EwEMapping key in _configurationService.Mappings(KeyDomain.Species))
                {
                    Species? species = key.ToObject<SURIMI.Datamodel.Species>(_configuration.IncludeVocabularies);
                    if (species != null)
                    {
                        BiomassGrid grid = new()
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
                                    // Return the biomass proportion in the FG for the current cell
                                    double biomassSpecies = _groupSpeciesProportions[iGroup].GetSpeciesBiomass(ir, ic, key, biomassCell);

                                    grid.BiomassCells.Add(new BiomassCell()
                                    {
                                        // JS 14Oct25: report cell centroid
                                        Latitude = bm.RowToLat((float)(ir + 0.5)),
                                        Longitude = bm.ColToLon((float)(ic + 0.5)),
                                        Biomass = biomassSpecies
                                    });
                                }
                        _biomassOut.BiomassGrids.Add(grid);
                    }
                }
            }
        }

        /// <summary>
        /// Prepare a snapshot of catch data for export. Only include internal gears, e.g., of catches produced by EwE.
        /// </summary>
        private void CacheCatchAndSalesData()
        {
            if (_catchOut == null)
                _catchOut = new();
            else
                _catchOut.DispositionGrids.Clear();

            if (_salesOut == null)
                _salesOut = new() { };
            else
                _salesOut.Clear();

            if (_configuration == null) return;

            cEcopathDataStructures ecopathds = m_core.EcopathDataStructures;
            cEcospaceDataStructures spaceds = m_core.EcospaceDataStructures;
            cEcospaceBasemap bm = m_core.EcospaceBasemap;

            // Tally absolute sales over all catch dispositions
            Dictionary<(int Group, int Fleet), (double Volume, double Value)> TotalSales = new();

            foreach (int iGroup in _configuration.FishedGroups)
            {
                EwEMapping? mlkGroup = _configurationService.FindMapping(iGroup, KeyDomain.Species);
                if (mlkGroup == null)
                    continue;

                float sppProp = mlkGroup.Proportion;

                for (int iFleet = 1; iFleet <= m_core.nFleets; iFleet++)
                {
                    // Only report fleets fished by EwE
                    if (!_configuration.ExternalFleets.Contains(iFleet))
                    {
                        // Tally up the catch dispositions for all the markets this gear code caters to
                        MultiLevelKey? mlkFleet = _configurationService.FindMapping(iFleet, KeyDomain.FleetSegment);
                        MultiLevelKey? mlkMarket = _configurationService.FindMapping(iFleet, KeyDomain.Market);

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
                                        (int Group, int Fleet) salekey = (iGroup, iFleet);
                                        (double Volume, double Value) saleTot = new(0, 0);
                                        if (TotalSales.ContainsKey(salekey))
                                            saleTot = TotalSales[salekey];
                                        else
                                            TotalSales[salekey] = saleTot;
                                        saleTot.Volume += (cellCatchesAbs - cellDeadDiscAbs);
                                        saleTot.Value += (cellCatchesAbs - cellDeadDiscAbs) * ecopathds.Market[iFleet, iGroup];
                                        TotalSales[salekey] = saleTot;

                                        // Prepare catch deposition. Note that EwE catches do NOT include live discards
                                        // ToDo_JS: Decide on the below. What is the framework expecting? 
                                        // cellCatchesAbs += cellLiveDiscAbs;

                                        catches[ir, ic] += cellCatchesAbs;
                                        livedisc[ir, ic] += cellLiveDiscAbs;
                                        deaddisc[ir, ic] += cellDeadDiscAbs;
                                        bHasData = true;
                                    }

                        // Finally prepare data for the framework
                        if (bHasData && mlkFleet != null && mlkGroup != null)
                        {
                            // Prepare disposition grid
#pragma warning disable CS8601 // Possible null reference assignment.
                            var grid = new DispositionGrid()
                            {
                                FleetSegment = mlkFleet.ToObject<SURIMI.Datamodel.FleetSegment>(_configuration.IncludeVocabularies),
                                Species = mlkGroup.ToObject<SURIMI.Datamodel.Species>(_configuration.IncludeVocabularies)
                            };
#pragma warning restore CS8601 // Possible null reference assignment.
                            for (int ir = 1; ir <= spaceds.InRow; ir++)
                                for (int ic = 1; ic <= spaceds.InCol; ic++)
                                    if (spaceds.Depth[ir, ic] > 0)
                                    {
                                        grid.DispositionCells.Add(new DispositionCell()
                                        {
                                            // JS 14Oct25: report cell centroid
                                            Latitude = bm.RowToLat((float)(ir + 0.5)),
                                            Longitude = bm.ColToLon((float)(ic + 0.5)),

                                            GrossCatchBiomass = catches[ir, ic],
                                            LiveDiscardsBiomass = livedisc[ir, ic],
                                            DeadDiscardsBiomass = deaddisc[ir, ic]
                                        });
                                    }
                            _catchOut.DispositionGrids.Add(grid);
                        }
                    }
                }
            }

            // Prepare sales
            for (int iFleet = 1; iFleet <= m_core.nFleets; iFleet++)
            {
                // Only report fleets fished by EwE
                if (!_configuration.ExternalFleets.Contains(iFleet))
                {
                    MultiLevelKey? mlkFleet = _configurationService.FindMapping(iFleet, KeyDomain.FleetSegment);
                    MultiLevelKey? mlkMarket = _configurationService.FindMapping(iFleet, KeyDomain.Market);

                    if (mlkFleet == null || mlkMarket == null)
                        continue;

                    var sales = new SalesSummary()
                    {
                        MarketCode = mlkMarket.GetField("marketcode").ToString(_configuration.IncludeVocabularies),
                        Currency = "EUR", // No conversion here
                        Sales = new List<Sale>()
                    };
                    foreach ((int Group, int Fleet) saleKey in TotalSales.Keys.Where(k => k.Fleet == iFleet))
                    {
                        MultiLevelKey? mlkSpecies = _configurationService.FindMapping(saleKey.Group, KeyDomain.Species);
                        if (mlkSpecies == null)
                            continue;

                        if (TotalSales.TryGetValue(saleKey, out var saleTot))
                        {
                            // Only report sales with a volume of at least the minimum sale quantity
                            if (saleTot.Volume >= _minimumSaleQuantity)
                            {
                                Sale s = new Sale()
                                {
                                    GearCode = mlkFleet.GetField(FishingFields.GearCode)!.ToString(_configuration.IncludeVocabularies),
                                    SpeciesCode = mlkSpecies.GetField(SpeciesFields.SpeciesCode)!.ToString(_configuration.IncludeVocabularies),
                                    CategoryCode = "",
                                    Quantity = saleTot.Volume,
                                    Value = saleTot.Value
                                };
                                sales.Sales.Add(s);
                            }
                        }
                    }
                    _salesOut.Add(sales);
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
            return dens * 1000 * m_core.EcospaceDataStructures.CellArea[irow, icol];
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
            float area = m_core.EcospaceDataStructures.CellArea[irow, icol];
            if (area == 0) area = 1; // Can happen
            return (float)kg / (area * 1000);
        }

        #endregion // Data interactions

        #region Internal - EwE interactions

        private void RunEcospace()
        {
            cCore.EcoSpaceInterfaceDelegate? dgt = null;
            m_core.RunEcospace(ref dgt);
        }

        private void ForceStop()
        {
            _logger.LogInformation("EwE - !! Force stop received");
            try
            {
                if (_thread != null && _thread.IsAlive)
                    _thread.Interrupt();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "In ForceStopEwE - exception");
            }

            RunState = RunStates.idle; // Manually reset to idle if needed
        }

        public IPlugin? GetPlugin(System.Type t)
        {
            IPluginManager pm = m_core.PluginManager;
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
            if (RunState == RunStates.stopping) return;

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
                        IntegratePrices();

                        cEcospaceDataStructures ds = m_core.EcospaceDataStructures;
                        if (ds.bInSpinUp)
                        {
                            // Tick
                            _spinUpStep += 1;
                            if (_spinUpStep % cCore.N_MONTHS == 0)
                                _logger.LogInformation("EwE - finished spinup year {SpinupYear}", (int)(_spinUpStep / cCore.N_MONTHS));
                        }
                        else
                        {
                            if (iTime % cCore.N_MONTHS == 0)
                                _logger.LogInformation("EwE - finished year {Year}", m_core.EcospaceTimestepToAbsoluteTime(iTime).Year);
                        }
                        break;

                    case cEcospaceBridgePlugin.EventType.BeginTimeStepPost:
                        // NOP
                        break;

                    case cEcospaceBridgePlugin.EventType.EndTimeStep:

                        // Do we need to pause at the start of the next time step?
                        if (MustPauseNext(iTime + 1))
                        {
                            // Prepare data for sending out
                            CacheBiomassData();
                            CacheCatchAndSalesData();

                            _logger.LogInformation("EwE - pausing at timestep {Timestep}", iTime + 1);
                            RunState = RunStates.waiting;
                            m_core.EcospacePaused = true;
                        }
                        break;

                    case cEcospaceBridgePlugin.EventType.EffortDistrPost:
                        // Effort has been distributed, and Ecospace is about to calculate ecosystem dynamics and fishing
                        // This is the perfect moment to integrate any remotely calculated catch dispositions into the running core
                        IntegrateCatchDispositions(iTime);
                        break;

                    case cEcospaceBridgePlugin.EventType.EndRun:
                        _logger.LogInformation("EwE - end run callback");

                        // Clear all modifications made to core data, if any
                        m_core.DiscardChanges();
                        // Correctly reset the state and clean up
                        RunState = RunStates.idle;
                        _thread = null;
                        break;

                    default:
                        // NOP
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EwE - exception on bridge callback {EventType}", e.ToString());
            }
        }

        private bool MustPauseNext(int iTime)
        {
            if (_configuration == null) throw new InvalidOperationException("Configuration is not set.");

            // Do not halt while in spin-up
            cEcospaceDataStructures ds = m_core.EcospaceDataStructures;
            if (_spinUpStep + 1 < _numSpinUpSteps) return false;
            DateTime dt = m_core.EcospaceTimestepToAbsoluteTime(iTime);
            return (dt.Year >= _configuration.StartYear);
        }

        /// <summary>
        /// It may occur that a group IS flagged as a stanza but that the
        /// key used to indicate the group does not have stanza data.
        /// In that case, each life stage is (for now) bluntly fished by
        /// the n lifestages in the stanza configuration
        /// </summary>
        /// <param name="iGroup"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private float StanzaWideModifier(int iGroup, MultiLevelKey key)
        {
            if (key == null)
                return 0f;

            var group = this.m_core.get_EcopathGroupInputs(iGroup);
            var iStanza = group.iStanza;
            bool hasStanzaKeys = (key.FieldNames.Count() > 1);

            if (iStanza < 0 || hasStanzaKeys)
                return 1f;

            var stanza = this.m_core.get_StanzaGroups(iStanza);
            return 1 / Math.Max(1, stanza.nLifeStages);
        }

        public Task<bool> UpdateRegulationsAsync(RegulationsSummary regulations)
        {
            // TODO
            return Task.FromResult(true);
        }

        public Task<FishingActivitySummary> GetFishingActivityAsync()
        {
            var summary = new FishingActivitySummary()
            {
                FishingActivities = new List<FishingActivity>()
                {
                    new FishingActivity()
                    {
                        FleetSegment = new FleetSegment() { GearCode = "OTB", CountryCode = "FRA" },
                        FishingActivityRatio = 0.5f
                    },
                    new FishingActivity()
                    {
                        FleetSegment = new FleetSegment() { GearCode = "ART", CountryCode = "ESP" },
                        FishingActivityRatio = 0.9f
                    }
                }
            };
            return Task.FromResult(summary);
        }


        /// <summary>
        /// Makes a snapshot of the EwE off-vessel prices and calculates the mean
        /// functional-group price for a market.
        ///
        /// The market-level functional-group prices are used as reference prices when
        /// translating market- and species-level price changes to EwE fleet ×
        /// functional-group prices.
        /// </summary>
        /// <param name="marketCode">
        /// The SURIMI market code associated with the selected fleets.
        /// </param>
        /// <param name="bWeighted">
        /// True to weight the mean prices by Ecopath landings; false to calculate
        /// an unweighted mean across fleets with landings.
        /// </param>
        /// <remarks>
        /// The SURIMI Market model provides prices by market and species. These prices
        /// are aggregated to functional-group prices before being applied to EwE.
        ///
        /// Row zero of the stored matrix contains the market-level reference price for
        /// each functional group. Rows one and above contain the original EwE
        /// fleet × functional-group prices.
        ///
        /// Fleet filtering by market is not yet implemented.
        /// </remarks>
        void CalculateBasePrices(bool bWeighted = true)
        {
            cEcopathDataStructures ds = m_core.EcopathDataStructures;
            _priceBridge = new PriceBridge(ds.Market);

            // First, set the fleet and market mappings
            foreach (EwEMapping key in _configurationService.Mappings(KeyDomain.Market))
            {
                _priceBridge.MapFleetToMarket(key.ToString(), key.Index);
            }


            // Second, add base prices to this
            for (int iFleet = 1; iFleet < ds.NumFleet; iFleet++)
            {
                for (int iGroup = 1; iGroup < ds.NumGroups; iGroup++)
                {
                    float price = ds.Market[iFleet, iGroup];
                    if (price > 0.0f)
                        _priceBridge.SetBasePrice(iGroup, iFleet, price, ds.Landing[iFleet, iGroup]);
                }
            }
        }
    }
        #endregion // Internal - EwE interactions
}
