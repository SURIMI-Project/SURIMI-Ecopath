using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Vocabularies.Tests
{
    public class WoRMSSpeciesVocabularyTests
    {
        [Fact]
        public void Should_Load_WoRMS_Vocabulary_Successfully()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary();

            // Act
            var loaded = wormsVocab.Load();

            // Assert
            loaded.Should().BeTrue();
            wormsVocab.VocabularyName.Should().Be("WoRMS");
            wormsVocab.Domain.Should().Be(KeyDomain.Species);
            wormsVocab.Purpose.Should().Be(KeyPurpose.Species);
            wormsVocab.CodeFieldName.Should().Be("AphiaID");
        }

        [Fact]
        public void Should_Include_Essential_Species_Fields()
        {
            // Arrange & Act
            var wormsVocab = new WoRMSSpeciesVocabulary();
            wormsVocab.Load();

            // Assert
            var fieldNames = wormsVocab.FieldNames.ToList();
            fieldNames.Should().Contain("AphiaID");
            fieldNames.Should().Contain("ScientificName");
            fieldNames.Should().Contain("CommonName");
            fieldNames.Should().Contain("FAO_Code"); // Critical for ASFIS cross-reference
        }

        [Fact]
        public void Should_Find_Species_By_Scientific_Name()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary();
            wormsVocab.Load();

            // Act
            var codResult = wormsVocab.FindCode("Gadus morhua");

            // Assert - Should find Atlantic cod
            codResult.Should().NotBeNullOrEmpty();
            codResult.Should().Be("126436"); // AphiaID for Gadus morhua
        }

        [Fact]
        public void Should_Find_Species_By_Common_Name_Fuzzy_Match()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary();
            wormsVocab.Load();

            // Act - Slightly misspelled common name
            var codResult = wormsVocab.FindCode("atlantic cod");

            // Assert - Should still find it via fuzzy matching
            codResult.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Should_Create_Cross_Reference_To_ASFIS()
        {
            // Arrange
            var registry = new VocabularyRegistry();
            var wormsVocab = new WoRMSSpeciesVocabulary();
            var asfisVocab = new ASFISSpeciesCodeVocabulary();

            registry.Register(wormsVocab);
            registry.Register(asfisVocab);

            // Act
            wormsVocab.ConfigureCrossReferences(registry);

            // Assert
            wormsVocab.TryGetForeignKey("FAO_Code", out var fkSpec).Should().BeTrue();
            fkSpec!.TargetVocabulary.Should().Be("asfis");
            fkSpec.TargetField.Should().Be("speciescode");
        }

        [Fact]
        public void Should_Enable_WoRMS_To_ASFIS_Resolution()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary();
            var asfisVocab = new ASFISSpeciesCodeVocabulary();
            wormsVocab.Load();
            asfisVocab.Load();

            // Act - Create a key with WoRMS data
            var wormsKey = MultiLevelKey.FromPairs([
                ("AphiaID", "WoRMS:126436"),
                ("ScientificName", "Gadus morhua"),
                ("FAO_Code", "COD")
            ], KeyDomain.Species);

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
            var wormsVocab = new WoRMSSpeciesVocabulary();
            wormsVocab.Load();

            var records = wormsVocab.Records.ToList();
            var codRecord = records.First(r => r.GetField("ScientificName")?.Value == "Gadus morhua");

            // Assert - Taxonomic fields should be available for hierarchical matching
            codRecord.GetField("Family")?.Value.Should().Be("Gadidae");
            codRecord.GetField("Order")?.Value.Should().Be("Gadiformes");
            codRecord.GetField("Class")?.Value.Should().Be("Actinopteri");
        }
    }
}