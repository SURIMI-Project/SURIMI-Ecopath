using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Vocabularies.Tests
{
    public class ISSCFGVocabularyTests
    {
        private readonly IVocabularyRegistry m_registry;
        private readonly IKeyFieldDescriptorIndexer m_keyFieldDescriptorIndexer;
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly IVocabularyMatcher m_vocabularyMatcher;
        private readonly ForeignKeyResolver m_fkResolver;
        private readonly IStrategyBasedMatcher m_matcher;


        public ISSCFGVocabularyTests()
        {
            m_registry = new VocabularyRegistry();
            m_keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
            m_fkResolver = new ForeignKeyResolver(m_registry, m_keyFieldDescriptorRegistry);

            m_vocabularyMatcher = new GenericVocabularyMatcher(m_registry, m_fkResolver, m_keyFieldDescriptorRegistry);

            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_registry, m_keyFieldDescriptorRegistry, m_vocabularyMatcher);
            m_matcher = new StrategyBasedMatcher(m_keyFieldDescriptorRegistry);   
        }

        [Fact]
        public void ISSCFG_Loads_And_Exposes_Core_Fields()
        {
            var v = new ISSCFGGearCodeVocabulary(m_fieldInferenceOrchestrator);
            v.Load(m_keyFieldDescriptorIndexer).Should().BeTrue();

            v.VocabularyName.Should().Be("ISSCFG");
            v.Domain.Should().Be(KeyDomain.FleetSegment);
            (v.Purpose & (KeyPurpose.Fleet | KeyPurpose.Gear)).Should().NotBe(0);
            v.CodeFieldName.Should().Be("GEAR_CODE"); // base normalizes names

            // Must have at least some rows
            v.Records.Should().NotBeEmpty();
        }

        [Fact]
        public void ISSCFG_FindCode_Fuzzy_On_Gear_Name()
        {
            var v = new ISSCFGGearCodeVocabulary(m_fieldInferenceOrchestrator);
            v.Load(m_keyFieldDescriptorIndexer).Should( ).BeTrue();

            // "Drifting longlines" := "DL"
            var code = v.FindCode("Drifting longlines", m_matcher);
            code.Should().Be("DL");
        }

        [Fact]
        public void Should_Correctly_Analyze_ISSCFG_Gear_Code_Field()
        {
            // Arrange
            var isscfgVocab = new ISSCFGGearCodeVocabulary(m_fieldInferenceOrchestrator);
            isscfgVocab.Load(m_keyFieldDescriptorIndexer);

            var indexer = new KeyFieldDescriptorIndexer();
            var descriptor = new KeyFieldDescriptor(isscfgVocab.CodeFieldName, KeyDomain.FleetSegment, KeyPurpose.Gear, FieldKind.Code);

            // Just to make sure, see comment below
            descriptor.UseAutoWeight.Should().BeTrue();
            descriptor.UserWeight.Should().Be(0);

            // Act
            var success = indexer.BuildIndex("GEAR_CODE", isscfgVocab.Records, descriptor, m_fieldInferenceOrchestrator);

            // Assert  
            success.Should().BeTrue();
            descriptor.Kind.Should().Be(FieldKind.Code); // Should detect as code due to CODE/NAME pair
            descriptor.Strategy.Should().HaveFlag(MatchStrategy.Exact); // Should use exact matching
            
            // This test failed when the test descriptor was initialized with a default weight of 1
            descriptor.Weight.Should().BeGreaterThan(5); // Should get good weight from analysis
        }
    }
}