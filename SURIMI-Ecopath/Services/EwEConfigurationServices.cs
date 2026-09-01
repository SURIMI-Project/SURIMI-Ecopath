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
using Eii.ControlledVocabularies.Vocabularies.Country;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;
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
        private readonly ASFISSpeciesCodeVocabulary m_asfisVocabulary;
        private readonly ISSCFGGearCodeVocabulary m_iSSCFGGearCodeVocabulary;
        private readonly ISO3166CountryCodeVocabulary m_iSO3166CountryCodeVocabulary;
        private readonly SURIMILifestageVocabulary m_SURIMILifestageVocabulary;
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

        public EwEConfigurationService(ASFISSpeciesCodeVocabulary asfisVocabulary, ISSCFGGearCodeVocabulary iSSCFGGearCodeVocabulary, ISO3166CountryCodeVocabulary iSO3166CountryCodeVocabulary, SURIMILifestageVocabulary sURIMILifestageVocabulary, IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry, IMultiLevelKeyFactory multiLevelKeyFactory, ILogger<EwEConfiguration> logger, IVocabularyRegistry vocabularies, IBlobStore blobStore, ISurimiContractToEwEService surimiContractToEwEService)
        {
            m_asfisVocabulary = asfisVocabulary;
            m_iSSCFGGearCodeVocabulary = iSSCFGGearCodeVocabulary;
            m_iSO3166CountryCodeVocabulary = iSO3166CountryCodeVocabulary;
            m_SURIMILifestageVocabulary = sURIMILifestageVocabulary;
            m_keyFieldDescriptorRegistry = keyFieldDescriptorRegistry;
            m_multiLevelKeyFactory = multiLevelKeyFactory;
            m_logger = logger;
            m_vocabularies = vocabularies;
            m_blobStore = blobStore;
            m_surimiContractToEwEService = surimiContractToEwEService;
        }

        public async Task<IEwEConfiguration> CreateConfigurationAsync(string scenarioName)
        {
            // adjust the Blobstore S3 directory for the scenario, so different scenarios can have different configurations and outputs
            m_blobStore.SetSubdirectory(scenarioName);

            // Load model
            var modelName = $"{scenarioName}.eiixml";
            if (!await m_blobStore.ExistsAsync(modelName, PathType.Input))    //This used to be @"GSA0607EwENBS.eiixml"
                throw new FileNotFoundException($"EwE model file '{modelName}' cannot be found");

            // If connected to a remote blob store, copy the model file locally to the Includes folder
            var localModelFile = await m_blobStore.CopyToLocalFileOrIgnoreAsync(modelName, PathType.Input);

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

            // Register the different species fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.SpeciesCode, KeyDomain.Species, KeyPurpose.Species, FieldKind.Code, true, 10));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Lifestage, KeyDomain.Species, KeyPurpose.Lifestage, FieldKind.Label, false, 3));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Length, KeyDomain.Species, KeyPurpose.Length, FieldKind.Label, false, 3));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Age, KeyDomain.Species, KeyPurpose.Age, FieldKind.Label, false, 3));

            // Register the different gear fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(FishingFields.GearCode, KeyDomain.FleetSegment, KeyPurpose.Gear, FieldKind.Code, true, 10));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(FishingFields.CountryCode, KeyDomain.FleetSegment, KeyPurpose.Country, FieldKind.Code, false, 3));

            // Register the different market fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(MarketFields.MarketCode, KeyDomain.FleetSegment, KeyPurpose.Market, FieldKind.Code, true, 10));

            // Register available look-up vocabularies
            m_vocabularies.Register(m_asfisVocabulary);
            m_vocabularies.Register(m_SURIMILifestageVocabulary);
            m_vocabularies.Register(m_iSSCFGGearCodeVocabulary);
            m_vocabularies.Register(m_iSO3166CountryCodeVocabulary);

            return configuration;
        }


        #region Persistence

        public async Task<bool> LoadAsync(IEwECore core, IEwEConfiguration configuration, SurimiContract surimiContract)
        {
            m_mappings.Clear();

            string cfgtext = GetConfigBucket(core).Remark;

            // Load fleet segment mappings from the semantics file

            await m_surimiContractToEwEService.ConvertSurimiContractToEwEAsync(m_mappings, core, surimiContract);

            // Register fleet segments as gear + countrycode pairs to match fleet + countrycode fishing
            //m_mappings.Add(new EwEMapping("gearcode=OTB; countrycode=ESP", KeyDomain.FleetSegment, 1, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=PS; countrycode=ESP", KeyDomain.FleetSegment, 2, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=LLS; countrycode=ESP", KeyDomain.FleetSegment, 3, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:ART; countrycode=ESP", KeyDomain.FleetSegment, 4, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:RECT; countrycode=ESP", KeyDomain.FleetSegment, 9, m_keyFieldDescriptorRegistry));

            //m_mappings.Add(new EwEMapping("gearcode=OTB; countrycode=FRA", KeyDomain.FleetSegment, 5, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=TM; countrycode=FRA", KeyDomain.FleetSegment, 6, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=PS; countrycode=FRA", KeyDomain.FleetSegment, 7, m_keyFieldDescriptorRegistry));
            //m_mappings.Add(new EwEMapping("gearcode=EwE:ART; countrycode=FRA", KeyDomain.FleetSegment, 8, m_keyFieldDescriptorRegistry));


            // Register fleet segments as gear + market code pairs to match fleet > market deliveries
            m_mappings.Add(new EwEMapping("gearcode=OTB; marketcode=ESALC", KeyDomain.Market, 1, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ESALC", KeyDomain.Market, 2, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=LLS; marketcode=ESALC", KeyDomain.Market, 3, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:ART; marketcode=ESALC", KeyDomain.Market, 4, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:RECT; marketcode=ESALC", KeyDomain.Market, 5, m_keyFieldDescriptorRegistry));

            m_mappings.Add(new EwEMapping("gearcode=OTB; marketcode=ESBRX", KeyDomain.Market, 6, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=TM; marketcode=ESBRX", KeyDomain.Market, 7, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ESBRX", KeyDomain.Market, 8, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:ART; marketcode=ESBRX", KeyDomain.Market, 9, m_keyFieldDescriptorRegistry));

            for (int iGroup = 1; iGroup <= core.nGroups; iGroup++)
                if (core.get_EcopathGroupInputs(iGroup).IsFished)
                    configuration.FishedGroups.Add(iGroup);

            if (surimiContract.Items == null || surimiContract.Items.Species == null || surimiContract.Items.Species.Count == 0)
            {
                throw new Exception("No species mappings found in configuration contract; species will not be resolved to functional groups");
            }

            ReadSpeciesMappings(core, configuration, surimiContract.Items.Species);
            ReadFleetMappings(core);

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

        public IEnumerable<EwEMappingMatch> ResolveEwEGroupsFromSpecies(string speciescode)
        {
            MultiLevelKey key = m_multiLevelKeyFactory.FromPairs([(SpeciesFields.SpeciesCode, speciescode)], KeyDomain.Species, m_keyFieldDescriptorRegistry);
            return ResolveEwEGroups(key);
        }

        public IEnumerable<EwEMappingMatch> ResolveEwEGroupsFromSpecies(SURIMI.Datamodel.Species species)
        {
            return ResolveEwEGroups(m_multiLevelKeyFactory.FromObject(species, KeyDomain.Species, m_keyFieldDescriptorRegistry));
        }

        /// <summary>
        /// Find all the groups from MLK species records.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// 
        public IEnumerable<EwEMappingMatch> ResolveEwEGroups(MultiLevelKey key)
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

        public IEnumerable<EwEMappingMatch> ResolveEwEFleetsFromMarket(string marketcode)
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
        private void ReadSpeciesMappings(IEwECore core, IEwEConfiguration configuration, List<Species> speciesList, bool writeCSV = false)
        {
            // The name of the vocabulary is implied here, but should be read from the species code
            IControlledVocabulary? vocSpecies = m_vocabularies.Get("asfis");
            IControlledVocabulary? vocLifeStage = m_vocabularies.Get("surimi.lifestage");

            if (vocSpecies == null || vocLifeStage == null) return;

            StreamWriter? csvWriter = null;
            if (writeCSV)
            {
                string csvFilePath = Path.GetFullPath(@".\EwE_functional-group_species.csv");

                csvWriter = new StreamWriter(csvFilePath);
                csvWriter.WriteLine("EwE_Group_No,EwE_Group_Name,Taxon_No,Common_Name,Genus,Species,FAO_Code,Lifestage_Code,Is_fished");
                m_logger.LogInformation("Writing species CSV file to {csvFilePath}", csvFilePath);
            }

            for (int iGroup = 1; iGroup <= core.nGroups; iGroup++)
            {
                cEcoPathGroupInput grp = core.get_EcopathGroupInputs(iGroup);
                if (grp.NTaxon == 0)
                {
                    if (csvWriter != null)
                    {
                        //csvWriter.WriteLine("EwE_Group_ID,EwE_Group_Name,Taxon_ID,Common_Name,Genus,Species,FAO_Code,Lifestage_Code,Is_fished");
                        csvWriter.WriteLine($"{grp.DBID},\"{grp.Name}\",,,,,,,{(grp.IsFished ? "yes" : "")}");
                    }
                }
                else
                {
                    for (int iTaxa = 1; iTaxa <= grp.NTaxon; iTaxa++)
                    {
                        cTaxon taxon = core.get_Taxon(grp.get_iTaxon(iTaxa));
                        string code = taxon.CodeFAO;
                        string ls = "";
                        float proportion = 1;

                        EwEMapping? key = null;

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
                            m_logger.LogInformation("Skipping taxon '{taxon}'; code '{code}' not in EwE_functional-group_species.csv", taxon.Common, code);
                            continue;
                        }

                        // Taxon refers to a multi-stanza configuration?
                        if (taxon.iStanza > 0)
                        {
                            // #Yes: find life stage code and biomass proportion    
                            ls = vocLifeStage.FindCode(grp.Name);
                            proportion = taxon.PropB / 100;
                        }
                        else
                        {
                            if (grp.iStanza > 0)
                            {
                                Console.WriteLine("EwE Config error: regular taxon {0} attached to stanza group {1}", taxon.DBID, taxon.iGroup);
                                continue;
                            }
                            proportion = 1;
                        } // if stanza

                        // Can add to mappings?
                        if (configuration.FishedGroups.Contains(iGroup) && !string.IsNullOrEmpty(code))
                        {
                            // #Yes: add a species code to the specific iGroup
                            key = new EwEMapping("", KeyDomain.Species, iGroup, m_keyFieldDescriptorRegistry, proportion);
                            key.SetField(SpeciesFields.SpeciesCode, vocSpecies.VocabularyName + ":" + code, m_keyFieldDescriptorRegistry);
                            if (!string.IsNullOrEmpty(ls))
                                key.SetField(SpeciesFields.Lifestage, vocLifeStage.VocabularyName + ":" + ls, m_keyFieldDescriptorRegistry);

                            this.m_mappings.Add(key);
                        }

                        if (csvWriter != null)
                        {
                            //csvWriter.WriteLine("EwE_Group_ID,EwE_Group_Name,Taxon_ID,Common_Name,Genus,Species,FAO_Code,Lifestage_Code,Is_Fished");
                            csvWriter.WriteLine($"{grp.Index},\"{grp.Name}\",{taxon.Index},\"{taxon.Name}\",\"{taxon.Genus}\",\"{taxon.Species}\",{code},{ls},{(grp.IsFished ? "yes" : "")}");
                        } // if csvWriter

                    } // for iTaxa
                } // if iTaxa
            } // for iGroup

            if (csvWriter != null)
            {
                csvWriter.Flush();
                csvWriter.Close();
            }
        }

        private void ReadFleetMappings(IEwECore core, bool writeCSV = false)
        {
            // The name of the vocabulary is implied here, but should be read from the fields
            IControlledVocabulary vocGear = m_vocabularies.Get("ISSCFG")!;
            IControlledVocabulary vocCountry = m_vocabularies.Get("ISO-3166")!;

            StreamWriter? csvWriter = null;
            if (writeCSV)
            {
                string csvFilePath = Path.GetFullPath(@".\EwE_functional-group_fisheries.csv");

                csvWriter = new StreamWriter(csvFilePath);
                csvWriter.WriteLine("EwE_Fleet_No,EwE_Fleet_Name, EwE_Group_No,EwE_Group_Name");
                m_logger.LogInformation("Writing fisheries CSV file to {csvFilePath}", csvFilePath);

                for (int iFleet = 1; iFleet <= core.nFleets; iFleet++)
                {
                    var fleet = (cEcopathFleetInput?)core.get_EcopathFleetInputs(iFleet);
                    if (fleet == null)
                    {
                        m_logger.LogWarning("Fleet index {iFleet} has no corresponding fleet input", iFleet);
                        continue;
                    }
                    for (int iGroup = 1; iGroup <= core.nGroups; iGroup++)
                    {
                        if (fleet.get_Landings(iGroup) > 0 || fleet.get_Discards(iGroup) > 0)
                        {
                            cEcoPathGroupInput grp = core.get_EcopathGroupInputs(iGroup);

                            //csvWriter.WriteLine("EwE_Group_ID,EwE_Group_Name,EwE_Fleet_ID,EwE_Fleet_Name");
                            csvWriter.WriteLine($"{iFleet},\"{fleet.Name}\",{grp.Index},\"{grp.Name}\"");
                        }
                    }
                }
                csvWriter.Flush();
                csvWriter.Close();
            }
        }



        #endregion // Smarts
    }
}
