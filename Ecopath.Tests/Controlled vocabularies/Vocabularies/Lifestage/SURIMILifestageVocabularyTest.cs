using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Vocabularies.Tests
{
    public class SURIMILifestageVocabularyTest
    {
        private readonly IVocabularyRegistry m_vocabularyRegistry;
        private readonly IKeyFieldDescriptorIndexer _keyFieldDescriptorIndexer;
        private GenericVocabularyMatcher m_matcher;                 // TODO: Why not IVocabularyMatcher??
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;

        public SURIMILifestageVocabularyTest()
        {
            m_vocabularyRegistry = new VocabularyRegistry();
            _keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            _keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            var foreignKeyResolver = new ForeignKeyResolver(m_vocabularyRegistry, m_keyFieldDescriptorRegistry);
            var vocabularyMatcher = new GenericVocabularyMatcher(m_vocabularyRegistry, foreignKeyResolver, m_keyFieldDescriptorRegistry);
            m_matcher = new GenericVocabularyMatcher(m_vocabularyRegistry, foreignKeyResolver, m_keyFieldDescriptorRegistry);
            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_vocabularyRegistry, m_keyFieldDescriptorRegistry, vocabularyMatcher);
        }

        [Fact]
        public void TestMatching()
        {
            SURIMILifestageVocabulary v1 = new(m_fieldInferenceOrchestrator);
            v1.Load(_keyFieldDescriptorIndexer).Should().BeTrue();
        }
    }
}