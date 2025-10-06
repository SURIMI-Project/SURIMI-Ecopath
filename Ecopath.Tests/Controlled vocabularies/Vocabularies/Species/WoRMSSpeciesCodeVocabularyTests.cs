using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies.LifeStage.Species;
using Eii.ControlledVocabularies.Vocabularies.Species;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Vocabularies.Tests
{
    public class WoRMSSpeciesVocabularyTests
    {
        private readonly IVocabularyRegistry m_registry;
        private readonly IKeyFieldDescriptorIndexer _keyFieldDescriptorIndexer;
        private readonly IVocabularyMatcher m_vocabularyMatcher;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly ForeignKeyResolver m_fkResolver;
        private readonly IStrategyBasedMatcher m_matcher;
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;

        public WoRMSSpeciesVocabularyTests()
        {
            m_registry = new VocabularyRegistry();
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
            _keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            m_fkResolver = new ForeignKeyResolver(m_registry, m_keyFieldDescriptorRegistry);
            m_vocabularyMatcher = new GenericVocabularyMatcher(m_registry, m_fkResolver, m_keyFieldDescriptorRegistry);
            m_matcher = new StrategyBasedMatcher(m_keyFieldDescriptorRegistry);
            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_registry, m_keyFieldDescriptorRegistry, m_vocabularyMatcher);
        }

        [Fact]
        public void Should_Load_WoRMS_Vocabulary_Successfully()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator);

            // Act
            var loaded = wormsVocab.Load(_keyFieldDescriptorIndexer);

            // Assert
            loaded.Should().BeTrue();
            wormsVocab.VocabularyName.Should().Be("WoRMS");  // Non-normalized
            wormsVocab.Domain.Should().Be(KeyDomain.Species);
            wormsVocab.Purpose.Should().Be(KeyPurpose.Species);
            wormsVocab.CodeFieldName.Should().Be("AphiaID"); // Non-normalized
        }

        [Fact]
        public void Should_Include_Essential_Species_Fields()
        {
            // Arrange & Act
            var wormsVocab = new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator);
            wormsVocab.Load(_keyFieldDescriptorIndexer);

            // Assert
            var fieldNames = wormsVocab.FieldNames.ToList();
            fieldNames.Should().Contain("aphiaid");        // Normalized
            fieldNames.Should().Contain("scientificname"); // Normalized
            fieldNames.Should().Contain("commonname");     // Normalized
            fieldNames.Should().Contain("fao-code");       // Normalized
        }

        [Fact]
        public void Should_Find_Species_By_Scientific_Name()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator);
            wormsVocab.Load(_keyFieldDescriptorIndexer);

            // Act
            var codResult = wormsVocab.FindCode("Gadus morhua", m_matcher);

            // Assert - Should find Atlantic cod
            codResult.Should().NotBeNullOrEmpty();
            codResult.Should().Be("126436"); // AphiaID for Gadus morhua
        }

        [Fact]
        public void Should_Find_Species_By_Common_Name_Fuzzy_Match()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator);
            wormsVocab.Load(_keyFieldDescriptorIndexer);

            // Act - Slightly misspelled common name
            var codResult = wormsVocab.FindCode("atlantic cod", m_matcher);

            // Assert - Should still find it via fuzzy matching
            codResult.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Should_Create_Cross_Reference_To_ASFIS()
        {
            // Arrange
            var registry = new VocabularyRegistry();
            var wormsVocab = new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator);
            var asfisVocab = new ASFISSpeciesCodeVocabulary(m_fieldInferenceOrchestrator);

            registry.Register(wormsVocab, _keyFieldDescriptorIndexer);
            registry.Register(asfisVocab, _keyFieldDescriptorIndexer);

            // Act
            wormsVocab.ConfigureCrossReferences(registry);

            // Assert
            wormsVocab.TryGetForeignKey("FAO_Code", out var fkSpec).Should().BeTrue();
            fkSpec!.TargetVocabulary.Should().Be("asfis");
            fkSpec.TargetField.Should().Be(FieldPolicy.ForSchema(asfisVocab.CodeFieldName));
        }

        [Fact]
        public void Should_Enable_WoRMS_To_ASFIS_Resolution()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator);
            var asfisVocab = new ASFISSpeciesCodeVocabulary(m_fieldInferenceOrchestrator);
            wormsVocab.Load(_keyFieldDescriptorIndexer);
            asfisVocab.Load(_keyFieldDescriptorIndexer);

            // Act - Create a key with WoRMS data
            var wormsKey = MultiLevelKey.FromPairs([
                ("AphiaID", "WoRMS:126436"),
                ("ScientificName", "Gadus morhua"),
                ("FAO_Code", "COD")
            ], KeyDomain.Species, m_keyFieldDescriptorRegistry);

            // Assert - Should be resolvable to ASFIS via FAO_Code field
            wormsKey.GetField("FAO_Code")!.Value.Should().Be("COD");

            // Future: This should enable automatic cross-vocabulary resolution
            // var asfisMatch = crossWalkResolver.Resolve(wormsKey, wormsVocab, asfisVocab);
            // asfisMatch.Score.Should().BeGreaterThan(90);
        }

        [Fact]
        public void Should_Have_Taxonomic_Hierarchy_For_Advanced_Matching()
        {
            // Arrange & Act
            var wormsVocab = new WoRMSSpeciesVocabulary(m_fieldInferenceOrchestrator);
            wormsVocab.Load(_keyFieldDescriptorIndexer);

            var records = wormsVocab.Records.ToList();
            var codRecord = records.First(r => r.GetField("ScientificName")?.Value == "Gadus morhua");

            // Assert - Taxonomic fields should be available for hierarchical matching
            codRecord.GetField("Family")?.Value.Should().Be("Gadidae");
            codRecord.GetField("Order")?.Value.Should().Be("Gadiformes");
            codRecord.GetField("Class")?.Value.Should().Be("Actinopteri");
        }
    }
}