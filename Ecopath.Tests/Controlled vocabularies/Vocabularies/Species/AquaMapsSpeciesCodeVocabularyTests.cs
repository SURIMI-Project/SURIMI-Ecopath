using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies.Species;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Vocabularies.Tests
{
    public class AquaMapsSpeciesCodeVocabularyTests
    {
        [Fact]
        public void Should_Load_AquaMaps_Vocabulary_Successfully()
        {
            // Arrange
            var vocab = new AquaMapsSpeciesCodeVocabulary();

            // Act
            var loaded = vocab.Load();

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
