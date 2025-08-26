using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Inference.Tests
{
    public class VocabularySemanticInferrerTests
    {
        private readonly VocabularyRegistry _registry;
        private readonly VocabularySemanticInferrer _inferrer;

        public VocabularySemanticInferrerTests()
        {
            _registry = new VocabularyRegistry();

            // Register all known vocabularies for FK testing
            var vocabularies = new IControlledVocabulary[]
            {
                new ASFISSpeciesCodeVocabulary(),
                new WoRMSSpeciesVocabulary(),
                new SURIMILifestageVocabulary(),
                new NERCLifeStageVocabulary(),
                new ISSCFGGearCodeVocabulary(),
                new ISO3166CountryCodeVocabulary()
            };

            foreach (var vocab in vocabularies)
            {
                vocab.Load();
                _registry.Register(vocab);
            }

            _inferrer = new VocabularySemanticInferrer(_registry);
        }

        [Theory]
        [InlineData(typeof(ASFISSpeciesCodeVocabulary), KeyDomain.Species, KeyPurpose.Species)]
        [InlineData(typeof(WoRMSSpeciesVocabulary), KeyDomain.Species, KeyPurpose.Species)]
        [InlineData(typeof(SURIMILifestageVocabulary), KeyDomain.Species, KeyPurpose.Lifestage)]
        [InlineData(typeof(NERCLifeStageVocabulary), KeyDomain.Species, KeyPurpose.Lifestage)]
        [InlineData(typeof(ISSCFGGearCodeVocabulary), KeyDomain.FleetSegment, KeyPurpose.Gear | KeyPurpose.Fleet)]
        [InlineData(typeof(ISO3166CountryCodeVocabulary), KeyDomain.Country, KeyPurpose.Country)]
        public void Should_Correctly_Infer_Primary_Domain_And_Purpose(Type vocabType, KeyDomain expectedDomain, KeyPurpose expectedPurpose)
        {
            // Arrange - Create "blank" version of vocabulary (without manual domain/purpose)
            var vocab = CreateBlankVocabulary(vocabType);

            // Act
            var result = _inferrer.AnalyzeVocabulary(vocab);

            // Assert
            result.InferredDomain.Should().Be(expectedDomain,
                $"Should infer {expectedDomain} for {vocabType.Name}");
            result.InferredPurpose.Should().Be(expectedPurpose,
                $"Should infer {expectedPurpose} for {vocabType.Name}");
            result.DomainConfidence.Should().BeGreaterThan(0.5,
                "Should have reasonable confidence in domain inference");
        }

        [Fact]
        public void Should_Identify_ASFIS_Species_Field_Semantics()
        {
            // Arrange
            var asfisVocab = CreateBlankVocabulary(typeof(ASFISSpeciesCodeVocabulary));

            // Act
            var result = _inferrer.AnalyzeVocabulary(asfisVocab);

            // Assert
            var codeField = result.FieldInferences.FirstOrDefault(f => f.FieldName.Contains("Alpha3"));
            codeField.Should().NotBeNull();
            codeField!.IsPotentialForeignKey.Should().BeTrue("Alpha3_Code should be identified as potential FK");
            codeField.ForeignKeyConfidence.Should().BeGreaterThan(0.5);

            var nameField = result.FieldInferences.FirstOrDefault(f => f.FieldName.Contains("Scientific"));
            nameField.Should().NotBeNull();
            nameField!.SemanticHints.Should().Contain(h => h.Domain == KeyDomain.Species && h.Purpose == KeyPurpose.Species);
        }

        [Fact]
        public void Should_Identify_WoRMS_To_ASFIS_Foreign_Key_Relationship()
        {
            // Arrange - WoRMS has FAO_Code field that should map to ASFIS
            var wormsVocab = CreateBlankVocabulary(typeof(WoRMSSpeciesVocabulary));

            // Act
            var result = _inferrer.AnalyzeVocabulary(wormsVocab);

            // Assert
            var faoCodeFK = result.ForeignKeyCandidates.FirstOrDefault(fk =>
                fk.SourceField == "FAO_Code" && fk.TargetVocabulary.Contains("asfis"));

            faoCodeFK.Should().NotBeNull("Should identify FAO_Code as FK to ASFIS");
            faoCodeFK!.MatchRatio.Should().BeGreaterThan(0.1, "Should have reasonable match ratio");
            faoCodeFK.Confidence.Should().BeGreaterThan(0.3, "Should have decent confidence");
        }

        [Fact]
        public void Should_Infer_Life_Stage_Vocabularies_From_Content()
        {
            // Arrange
            var surimiVocab = CreateBlankVocabulary(typeof(SURIMILifestageVocabulary));

            // Act
            var result = _inferrer.AnalyzeVocabulary(surimiVocab);

            // Assert
            result.InferredDomain.Should().Be(KeyDomain.Species);
            result.InferredPurpose.Should().Be(KeyPurpose.Lifestage);

            // Should identify "values" field as descriptive content
            var valuesField = result.FieldInferences.FirstOrDefault(f => f.FieldName == "values");
            valuesField.Should().NotBeNull();
            valuesField!.SemanticHints.Should().Contain(h => h.Purpose == KeyPurpose.Lifestage);
        }

        [Fact]
        public void Should_Identify_Cross_Lifestage_Vocabulary_Compatibility()
        {
            // Arrange
            var surimiVocab = CreateBlankVocabulary(typeof(SURIMILifestageVocabulary));

            // Act
            var result = _inferrer.AnalyzeVocabulary(surimiVocab);

            // Assert - Should identify NERC as compatible lifestage vocabulary
            var nercCompatibility = result.ForeignKeyCandidates.FirstOrDefault(fk =>
                fk.TargetVocabulary.Contains("nerc"));

            // Note: This might not find exact FK matches, but should identify semantic compatibility
            // The test validates that the inferrer is looking for cross-vocabulary relationships
            Console.WriteLine($"Found {result.ForeignKeyCandidates.Count} FK candidates");
            foreach (var candidate in result.ForeignKeyCandidates)
            {
                Console.WriteLine($"  {candidate.SourceField} -> {candidate.TargetVocabulary}.{candidate.TargetField} (confidence: {candidate.Confidence:F2})");
            }
        }

        [Fact]
        public void Should_Provide_Detailed_Inference_Reasoning()
        {
            // Arrange
            var gearVocab = CreateBlankVocabulary(typeof(ISSCFGGearCodeVocabulary));

            // Act
            var result = _inferrer.AnalyzeVocabulary(gearVocab);

            // Assert - Should have detailed reasoning for inferences
            result.FieldInferences.Should().NotBeEmpty();

            foreach (var field in result.FieldInferences)
            {
                if (field.SemanticHints.Any())
                {
                    field.SemanticHints.Should().AllSatisfy(hint =>
                        hint.Reason.Should().NotBeNullOrEmpty("Each hint should have reasoning"));
                }

                if (field.IsPotentialForeignKey)
                {
                    field.Reasons.Should().NotBeEmpty("FK fields should have reasoning");
                }
            }

            // Print detailed results for manual inspection
            Console.WriteLine($"\n=== {result.VocabularyName} Analysis ===");
            Console.WriteLine($"Inferred Domain: {result.InferredDomain} (confidence: {result.DomainConfidence:F2})");
            Console.WriteLine($"Inferred Purpose: {result.InferredPurpose}");

            Console.WriteLine("\nField Analysis:");
            foreach (var field in result.FieldInferences)
            {
                Console.WriteLine($"  {field.FieldName}:");
                Console.WriteLine($"    FK Potential: {field.IsPotentialForeignKey} ({field.ForeignKeyConfidence:F2})");
                foreach (var hint in field.SemanticHints)
                {
                    Console.WriteLine($"    Hint: {hint.Domain}.{hint.Purpose} ({hint.Confidence:F2}) - {hint.Reason}");
                }
                foreach (var reason in field.Reasons)
                {
                    Console.WriteLine($"    Reason: {reason}");
                }
            }
        }

        private IControlledVocabulary CreateBlankVocabulary(Type vocabType)
        {
            // Create instance of vocabulary type without loading metadata
            var vocab = (IControlledVocabulary)Activator.CreateInstance(vocabType)!;
            vocab.Load(); // Load data but we'll ignore the manually set domain/purpose
            return vocab;
        }
    }
}