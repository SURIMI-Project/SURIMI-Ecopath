using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Inference.Vocabulary;
using Eii.ControlledVocabularies.Inference.Vocabulary.Strategies;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies;
using Eii.ControlledVocabularies.Vocabularies.Country;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.LifeStage.Species;
using Eii.ControlledVocabularies.Vocabularies.Species;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace ControlledVocabularies.Inference.Tests
{
    public class VocabularyInferenceSmokeTests
    {
        private readonly IVocabularyRegistry m_registry;
        private readonly IKeyFieldDescriptorIndexer m_keyFieldDescriptorIndexer;
        private readonly VocabularyInferenceOrchestrator m_orchestrator;
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly ForeignKeyResolver m_fkResolver;
        private readonly IVocabularyMatcher m_vocabularyMatcher;

        public VocabularyInferenceSmokeTests()
        {
            m_registry = new VocabularyRegistry();
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
            m_fkResolver = new ForeignKeyResolver(m_registry, m_keyFieldDescriptorRegistry);
            m_vocabularyMatcher = new GenericVocabularyMatcher(m_registry, m_fkResolver, m_keyFieldDescriptorRegistry);
            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_registry, m_keyFieldDescriptorRegistry, m_vocabularyMatcher);
            m_keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();


            // Register known vocabs
            var vocabs = new IControlledVocabulary[]
            {
                new ASFISSpeciesCodeVocabulary(m_fieldInferenceOrchestrator),
                new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator),
                new SURIMILifestageVocabulary(m_fieldInferenceOrchestrator),
                new NERCLifeStageVocabulary(m_fieldInferenceOrchestrator),
                new ISSCFGGearCodeVocabulary(m_fieldInferenceOrchestrator),
                new ISO3166CountryCodeVocabulary(m_fieldInferenceOrchestrator)
            };

            var i = 0;
            while (i < vocabs.Length)
            {
                m_registry.Register(vocabs[i], m_keyFieldDescriptorIndexer);
                i++;
            }

            // Build inference orchestrator with strategies
            m_orchestrator = new VocabularyInferenceOrchestrator();
            m_orchestrator.RegisterStrategy(new NamePatternVocabularyStrategy(m_registry));
            m_orchestrator.RegisterStrategy(new ForeignKeyDiscoveryStrategy(m_registry));
        }

        [Theory]
        [InlineData(typeof(ASFISSpeciesCodeVocabulary), KeyDomain.Species, KeyPurpose.Species)]
        [InlineData(typeof(WoRMSSpeciesVocabulary), KeyDomain.Species, KeyPurpose.Species)]
        [InlineData(typeof(SURIMILifestageVocabulary), KeyDomain.Species, KeyPurpose.Lifestage)]
        [InlineData(typeof(NERCLifeStageVocabulary), KeyDomain.Species, KeyPurpose.Lifestage)]
        [InlineData(typeof(ISSCFGGearCodeVocabulary), KeyDomain.FleetSegment, KeyPurpose.Gear | KeyPurpose.Fleet)]
        [InlineData(typeof(ISO3166CountryCodeVocabulary), KeyDomain.Country, KeyPurpose.Country)]
        public void Should_Infer_Primary_Domain_And_Purpose(Type vocabType, KeyDomain expectedDomain, KeyPurpose expectedPurpose)
        {
            var vocab = (IControlledVocabulary)Activator.CreateInstance(vocabType)!;
            vocab.Load(m_keyFieldDescriptorIndexer);

            var result = m_orchestrator.Analyze(vocab, null, registry: m_registry);

            result.InferredDomain.Should().Be(expectedDomain);
            result.InferredPurpose.Should().Be(expectedPurpose);
            result.DomainConfidence.Should().BeGreaterThan(0.3); // loose smoke threshold
        }

        [Fact]
        public void Should_Find_WoRMS_to_ASFIS_FK_Candidate()
        {
            var worms = new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator);
            var asfis = new ASFISSpeciesCodeVocabulary(m_fieldInferenceOrchestrator);
            worms.Load(m_keyFieldDescriptorIndexer);
            asfis.Load(m_keyFieldDescriptorIndexer);

            // Only want to explore asfis here
            m_registry.Clear();
            //_registry.Register(worms);
            m_registry.Register(asfis, m_keyFieldDescriptorIndexer);

            // This test will fail as the WoRMS CSV file is still missing the FAO key
            var res = m_orchestrator.Analyze(worms, null, registry: m_registry);

            bool found = false;
            foreach (var fk in res.ForeignKeyCandidates) 
            {
                if (fk == null) continue;

                // We only normalize the target vocabulary name for robust comparison
                var targetName = fk.TargetVocabulary ?? "";
                var targetSchema = FieldPolicy.ForSchema(targetName);

                if (string.Equals(targetSchema, "asfis", StringComparison.Ordinal))
                {
                    found = true;
                    fk.Score.Should().BeGreaterThan(10);
                    fk.Confidence.Should().BeGreaterThan(0.2);
                    break;
                }
            }

            found.Should().BeTrue("Expected an FK candidate from WoRMS FAO_Code to ASFIS Alpha3_Code");
        }
    }
}
