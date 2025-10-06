using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace ControlledVocabularies.Match
{
    public class GenericVocabularyMatcherTests
    {
        private readonly IVocabularyRegistry m_registry;
        private readonly IKeyFieldDescriptorIndexer m_keyFieldDescriptorIndexer;
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly ForeignKeyResolver m_fkResolver;
        private readonly IVocabularyMatcher m_vocabularyMatcher;

        public GenericVocabularyMatcherTests()
        {
            m_registry = new VocabularyRegistry();
            m_keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
            m_fkResolver = new ForeignKeyResolver(m_registry, m_keyFieldDescriptorRegistry);
            m_vocabularyMatcher = new GenericVocabularyMatcher(m_registry, m_fkResolver, m_keyFieldDescriptorRegistry);
            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_registry, m_keyFieldDescriptorRegistry, m_vocabularyMatcher);

        }

        [Fact]
        public void TestMatchCompatibleVocabularies()
        {
            SURIMILifestageVocabulary v1 = new(m_fieldInferenceOrchestrator);
            NERCLifeStageVocabulary v2 = new(m_fieldInferenceOrchestrator);

            MatchHelpers.CanMatch(v1, v2).Should().BeTrue();
        }

        [Fact]
        public void TestMatchIncompatibleVocabularies()
        {
            SURIMILifestageVocabulary v1 = new(m_fieldInferenceOrchestrator);
            ASFISSpeciesCodeVocabulary v2 = new(m_fieldInferenceOrchestrator);

            MatchHelpers.CanMatch(v1, v2).Should().BeFalse();
        }

        [Fact]
        public void TestFindMatches()
        {
            SURIMILifestageVocabulary v1 = new(m_fieldInferenceOrchestrator);
            v1.Load(m_keyFieldDescriptorIndexer).Should().BeTrue();

            NERCLifeStageVocabulary v2 = new(m_fieldInferenceOrchestrator);
            v2.Load(m_keyFieldDescriptorIndexer).Should().BeTrue();

            GenericVocabularyMatcher m = new(m_registry, m_fkResolver, m_keyFieldDescriptorRegistry);
            MultiLevelKey key = MultiLevelKey.FromPairs([(SpeciesFields.Lifestage, "juvenile")], KeyDomain.Species, m_keyFieldDescriptorRegistry, strict:false);

            // This is a bloody big deal: a SURIMI lifestage code can be matched to a record from a totally independent vocabulary
            var result = m.Match(key, v1, v2);
            result.Score.Should().BeGreaterThan(70);
        }
    }
}