using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies.Species;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace ControlledVocabularies.Vocabularies.Tests
{
    public class AquaMapsSpeciesCodeVocabularyTests
    {
        private readonly IVocabularyRegistry m_registry;
        private readonly IKeyFieldDescriptorIndexer m_keyFieldDescriptorIndexer;
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;
        private readonly IVocabularyMatcher m_vocabularyMatcher;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;


        public AquaMapsSpeciesCodeVocabularyTests()
        {
            m_registry = new VocabularyRegistry();
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
            m_keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_registry, m_keyFieldDescriptorRegistry, m_vocabularyMatcher);
        }

        [Fact]
        public void Should_Load_AquaMaps_Vocabulary_Successfully()
        {
            // Arrange
            var vocab = new AquaMapsSpeciesCodeVocabulary(m_fieldInferenceOrchestrator);

            // Act
            var loaded = vocab.Load(m_keyFieldDescriptorIndexer);

            // Assert
            loaded.Should().BeTrue();
            vocab.VocabularyName.Should().Be("AquaMaps.species");  // Non-normalized
            vocab.Domain.Should().Be(KeyDomain.Species);
            vocab.Purpose.Should().Be(KeyPurpose.Species);
            vocab.CodeFieldName.Should().Be("SPECIESID");    // Non-normalized

            vocab.Records.Count().Should().BeGreaterThan(1000);
            MultiLevelKey record = vocab.Records.First();
            record.GetField("ScientificName")!.Value.Should().NotBeNull();
        }
    }
}
