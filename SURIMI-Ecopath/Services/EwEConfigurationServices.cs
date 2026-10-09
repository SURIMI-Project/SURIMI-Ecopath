using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Eii.BlobStore;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Context;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Resolve;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies;
using EwECore;
using EwECore.Auxiliary;
using SURIMI.Datamodel;

namespace Ecopath.Services
{

    public partial class EwEConfigurationService : IEwEConfigurationService
    {
        /// <summary>
        /// MultiLevelKey -> EwE item mapping
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>"Species.SpeciesCode=<value>{;Species.Stage=<value>}" -> iGroup + proportion"</item>
        /// <item>"FleetSegment.GearCode=<value>;FleetSegment.countrycode=<value>" -> iFleet</item>
        /// <item>"GearCode=<value>;MarketCode=<value>" -> iFleet</item>
        /// </list>
        /// </remarks>
        private readonly List<EwEMapping> m_mappings = new();
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly IVocabularyRegistry m_vocabularies;
        private readonly IMultiLevelKeyFactory m_multiLevelKeyFactory;
        private readonly ILogger<EwEConfiguration> m_logger;
        private readonly IBlobStore m_blobStore;
        private readonly ISurimiContractToEwEService m_surimiContractToEwEService;

        private class EwEMappingComparer : IComparer<EwEMapping>
        {
            public int Compare(EwEMapping? x, EwEMapping? y)
            {
                if (x == null && y == null)
                    return 0;
                else if (x == null)
                    return -1;
                else if (y == null)
                    return 1;

                if (x.Index < y.Index) return -1;
                if (x.Index > y.Index) return 1;
                return string.Compare(x.ToString(), y.ToString());
            }
        }

        public EwEConfigurationService(IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry, IMultiLevelKeyFactory multiLevelKeyFactory, ILogger<EwEConfiguration> logger, IVocabularyRegistry vocabularies, IBlobStore blobStore, ISurimiContractToEwEService surimiContractToEwEService)
        {
            m_keyFieldDescriptorRegistry = keyFieldDescriptorRegistry;
            m_multiLevelKeyFactory = multiLevelKeyFactory;
            m_logger = logger;
            m_vocabularies = vocabularies;
            m_blobStore = blobStore;
            m_surimiContractToEwEService = surimiContractToEwEService;
        }

        public async Task<IEwEConfiguration> CreateConfigurationAsync(string scenarioName, CancellationToken cancellationToken)
        {
            // adjust the Blobstore S3 directory for the scenario, so different scenarios can have different configurations and outputs
            m_blobStore.SetSubdirectory(scenarioName);

            // Load model
            var modelName = $"{scenarioName}.eiixml";
            if (!await m_blobStore.ExistsAsync(modelName, PathType.Input))
                throw new FileNotFoundException($"EwE model file '{modelName}' cannot be found");

            // If connected to a remote blob store, copy the model file locally to the Includes folder
            await m_blobStore.CopyToLocalDirectoryOrIgnoreAsync("", PathType.Input, cancellationToken);
            var localModelFile = Path.Combine(m_blobStore.LocalInputRoot, modelName);

            var configuration = new EwEConfiguration()
            {
                ModelName = modelName,
                EcosimScenario = 1,
                EcosimTimeSeries = 0,
                EcospaceScenario = 1,
                MaxRunYears = 25,
#if DEBUG
                // Just to speed up the process of testing :P
                SpinupYears = 1,
                StartYear = 2001,
#else
                SpinupYears = 10,
                StartYear = 2013, // The year that the simulation starts
#endif
                LocalModelFile = localModelFile
            };

            var reg = ModelContextDescriptorRegistry.Create();
            var context = new ModelContext(reg, m_multiLevelKeyFactory);

            // ToDo: make this real once the model has loaded
            context.SetModelName("EwE Demo");
            context.SetAreaName("NE Atlantic");
            context.SetBoundingBox(35.0, -25.0, 70.0, 20.0);
            context.SetYears(1990, 2020);
            context.SetTimestamp(DateTime.UtcNow);

            return configuration;
        }


        #region Persistence

        public async Task<bool> LoadAsync(IEwECore core, IEwEConfiguration configuration, SurimiContract surimiContract)
        {
            m_mappings.Clear();

            string cfgtext = GetConfigBucket(core).Remark;

            // Load fleet segment mappings from the semantics file
            WriteSpeciesMappingsFromModel(core, configuration, surimiContract.Items.Species);

            await m_surimiContractToEwEService.ReadSemanticMappings(m_mappings, core, surimiContract);

            //// Register fleet segments as gear + countrycode pairs to match fleet + countrycode fishing
            //// ToDo: obtain from Semantic Registry instead of hardcoding here
            //m_mappings.Add(new EwEMapping("gearcode=OTB; countrycode=ESP", KeyDomain.FleetSegment, 1, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=PS; countrycode=ESP", KeyDomain.FleetSegment, 2, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=LLS; countrycode=ESP", KeyDomain.FleetSegment, 3, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:ART; countrycode=ESP", KeyDomain.FleetSegment, 4, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:RECT; countrycode=ESP", KeyDomain.FleetSegment, 9, m_keyFieldDescriptorRegistry));

            //m_mappings.Add(new EwEMapping("gearcode=OTB; countrycode=FRA", KeyDomain.FleetSegment, 5, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=TM; countrycode=FRA", KeyDomain.FleetSegment, 6, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=PS; countrycode=FRA", KeyDomain.FleetSegment, 7, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:ART; countrycode=FRA", KeyDomain.FleetSegment, 8, m_keyFieldDescriptorRegistry));


            //// Register fleet segments as gear + market code pairs to match fleet > market deliveries
            //m_mappings.Add(new EwEMapping("gearcode=OTB; marketcode=ES", KeyDomain.Market, 1, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ES", KeyDomain.Market, 2, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=LLS; marketcode=ES", KeyDomain.Market, 3, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:ART; marketcode=ES", KeyDomain.Market, 4, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:RECT; marketcode=ES", KeyDomain.Market, 9, m_keyFieldDescriptorRegistry));

            //m_mappings.Add(new EwEMapping("gearcode=OTB; marketcode=FR", KeyDomain.Market, 5, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=TM; marketcode=FR", KeyDomain.Market, 6, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=FR", KeyDomain.Market, 7, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:ART; marketcode=FR", KeyDomain.Market, 8, m_keyFieldDescriptorRegistry));

            if (surimiContract.Items == null || surimiContract.Items.Species == null || surimiContract.Items.Species.Count == 0)
            {
                throw new Exception("No species mappings found in configuration contract; species will not be resolved to functional groups");
            }


            m_mappings.Sort(new EwEMappingComparer());

            return true;
        }

        public bool Save(cCore core)
        {
            return true;
        }

        private cAuxiliaryData GetConfigBucket(IEwECore core)
        {
            cEcospaceModelParameters parms = core.EcospaceModelParameters;
            return core.AuxillaryData("SURIMI_link_" + parms.DBID);
        }

        #endregion // Persistence

        #region Consulting the registry

        public IEnumerable<EwEMappingMatch> ResolveEwEGroupFromSpecies(string speciescode)
        {
            MultiLevelKey key = m_multiLevelKeyFactory.FromPairs([(SpeciesFields.SpeciesCode, speciescode)], KeyDomain.Species, m_keyFieldDescriptorRegistry);
            return ResolveEwEGroup(key);
        }

        public IEnumerable<EwEMappingMatch> ResolveEwEGroupFromSpecies(SURIMI.Datamodel.Species species)
        {
            return ResolveEwEGroup(m_multiLevelKeyFactory.FromObject(species, KeyDomain.Species, m_keyFieldDescriptorRegistry));
        }

        /// <summary>
        /// Find all the groups from MLK species records.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// 
        public IEnumerable<EwEMappingMatch> ResolveEwEGroup(MultiLevelKey key)
        {
            if (key.Domain != KeyDomain.Species)
                yield break;

            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptorRegistry.GetAll(KeyDomain.Species));
            foreach (var match in resolver.FindAllMatches(key))
                yield return new EwEMappingMatch((EwEMapping)match.MatchedKey, match.Score);
        }

        /// <summary>
        /// Find the EwE fleets that match a given SURIMI fleet segment
        /// </summary>
        /// <param name="fleetsegment"></param>
        /// <returns></returns>
        public IEnumerable<EwEMappingMatch> ResolveEwEFleets(SURIMI.Datamodel.FleetSegment fleetsegment)
        {
            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptorRegistry.GetAll(KeyDomain.FleetSegment));
            foreach (var match in resolver.FindAllMatches(m_multiLevelKeyFactory.FromObject(fleetsegment, KeyDomain.FleetSegment, m_keyFieldDescriptorRegistry)))
                yield return new EwEMappingMatch((EwEMapping)match.MatchedKey, match.Score);
        }

        public IEnumerable<EwEMappingMatch> ResolveEwEFleet(string marketcode)
        {
            MultiLevelKey key = m_multiLevelKeyFactory.FromPairs([(MarketFields.MarketCode, marketcode)], KeyDomain.Market, m_keyFieldDescriptorRegistry);
            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptorRegistry.GetAll(KeyDomain.Market));
            foreach (var match in resolver.FindAllMatches(key))
                yield return new EwEMappingMatch((EwEMapping)match.MatchedKey, match.Score);
        }

        /// <inheritdoc />
        public EwEMapping? FindMapping(int iIndex, KeyDomain domain)
        {
            return m_mappings.FirstOrDefault(m => m.Index == iIndex && m.Domain == domain);
        }

        #endregion // Consulting the registry

        #region Mappings

        /// <inheritdoc />
        public IEnumerable<EwEMapping> Mappings(KeyDomain domain)
        {
            foreach (var kvp in m_mappings.Where(n => n.Domain == domain))
                yield return kvp;
        }

        #endregion // Mappings

        #region Smarts 


        /// <summary>
        /// Load the species key -> FN group mappings from the model. 
        /// </summary>
        /// <param name="core"></param>
        /// <remarks>
        /// The species mappings are intended for all fisheries-related accounting in SURIMI, and
        /// only applies to fished groups. Species that are not fished, or fished functional groups 
        /// without taxonomic records / species attached, are not registered here.
        /// </remarks>
        private void WriteSpeciesMappingsFromModel(IEwECore core, IEwEConfiguration configuration, List<Species> speciesList)
        {
            // The name of the vocabulary is implied here, but should be read from the species code
            IControlledVocabulary? vocSpecies = m_vocabularies.Get("asfis");
            IControlledVocabulary? vocLifeStage = m_vocabularies.Get("surimi.lifestage");

            if (vocSpecies == null || vocLifeStage == null) return;

            string csvFilePath = Path.GetFullPath(@".\speciesmappings.csv");
            StreamWriter writerContract = new StreamWriter(csvFilePath);

            foreach (int iGroup in configuration.FishedGroups)
            {
                cEcoPathGroupInput grp = core.get_EcopathGroupInputs(iGroup);
                for (int iTaxa = 1; iTaxa <= grp.NTaxon; iTaxa++)
                {
                    cTaxon taxon = core.get_Taxon(grp.get_iTaxon(iTaxa));
                    string code = taxon.CodeFAO;
                    string ls = "";

                    if (String.IsNullOrEmpty(code))
                    {
                        // Make robust to encoding imperfections
                        string common = FieldPolicy.ForValue(taxon.Common, FieldKind.Label);
                        if (string.IsNullOrEmpty(common))
                            common = taxon.Genus + ' ' + taxon.Species;
                        code = vocSpecies.FindCode(common);

                        // Last resort: try spp.    
                        if (string.IsNullOrEmpty(code))
                        {
                            common = common.Substring(0, Math.Max(0, common.LastIndexOf(' '))) + " spp.";
                            code = vocSpecies.FindCode(common);
                        }
                    }

                    if (!speciesList.Any(s => s.SpeciesCode == code))
                    {
                        m_logger.LogInformation("Skipping taxon '{taxon}'; code '{code}' not found in SURIMI contract", taxon.Common, code);
                        continue;
                    }

                    writerContract.WriteLine(",");
                    writerContract.WriteLine("{");
                    writerContract.Write("    \"Source\": \"speciescode=" + code);
                    if (taxon.iStanza > 0)
                    {
                        ls = vocLifeStage.FindCode(grp.Name);
                        writerContract.Write(";lifestage=" + ls);
                    }
                    writerContract.WriteLine("\"");
                    writerContract.WriteLine("    \"Target\": \"model=group;DBID=" + grp.DBID + "\"");
                    writerContract.Write("}");

                } // for iTaxa
            } // for iGroup

            writerContract.Flush();
            writerContract.Close();
        }

        #endregion // Smarts
    }
}
