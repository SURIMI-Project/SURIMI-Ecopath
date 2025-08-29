using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;
using ControlledVocabularies.Utils;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Inference.Tests
{
    /// <summary>
    /// Updated tests to align with normalized field names (FieldPolicy.ForSchema)
    /// and to keep assertions explicit and legible.
    /// </summary>
    public class VocabularySemanticInferrerTests
    {
        private readonly VocabularyRegistry _registry;
        private readonly VocabularyInferenceEngine _inferrer;

        public VocabularySemanticInferrerTests()
        {
            _registry = new VocabularyRegistry();

            // Register core vocabularies used in inference tests
            IControlledVocabulary[] vocabularies = new IControlledVocabulary[]
            {
                new ASFISSpeciesCodeVocabulary(),
                new WoRMSSpeciesVocabulary(),
                // If available in the solution, uncomment these to enable the life-stage tests
                // new SURIMILifestageVocabulary(),
                // new NERCLifeStageVocabulary(),
                // new ISSCFGGearCodeVocabulary(),
                // new ISO3166CountryCodeVocabulary()
            };

            for (int i = 0; i < vocabularies.Length; i++)
            {
                vocabularies[i].Load();
                _registry.Register(vocabularies[i]);
            }

            _inferrer = new VocabularyInferenceEngine(_registry);
        }

        [Theory]
        [InlineData(typeof(ASFISSpeciesCodeVocabulary), KeyDomain.Species, KeyPurpose.Species)]
        [InlineData(typeof(WoRMSSpeciesVocabulary), KeyDomain.Species, KeyPurpose.Species)]
        public void Should_Correctly_Infer_Primary_Domain_And_Purpose(Type vocabType, KeyDomain expectedDomain, KeyPurpose expectedPurpose)
        {
            // Arrange
            var vocab = (IControlledVocabulary)Activator.CreateInstance(vocabType)!;
            vocab.Load();

            // Act
            var result = _inferrer.AnalyzeVocabulary(vocab);

            // Assert
            result.InferredDomain.Should().Be(expectedDomain);
            result.InferredPurpose.Should().Be(expectedPurpose);
            result.DomainConfidence.Should().BeGreaterThan(0.5);
        }

        [Fact]
        public void Should_Identify_ASFIS_Species_Field_Semantics_With_Normalized_FieldNames()
        {
            // Arrange
            var asfisVocab = new ASFISSpeciesCodeVocabulary();
            asfisVocab.Load();

            // Act
            var result = _inferrer.AnalyzeVocabulary(asfisVocab);

            // Assert (use normalized names)
            string codeFieldName = FieldPolicy.ForSchema("Alpha3_Code");      // => "alpha3-code"
            string nameFieldName = FieldPolicy.ForSchema("Scientific_Name");  // => "scientific-name"

            var codeField = FindField(result.FieldInferences, codeFieldName);
            codeField.Should().NotBeNull();
            codeField!.IsPotentialForeignKey.Should().BeTrue();
            codeField.ForeignKeyConfidence.Should().BeGreaterThan(0.5);

            var nameField = FindField(result.FieldInferences, nameFieldName);
            nameField.Should().NotBeNull();
            nameField!.SemanticHints.Should().Contain(h => h.Domain == KeyDomain.Species && (h.Purpose & KeyPurpose.Species) != 0);
        }

        [Fact]
        public void Should_Identify_WoRMS_To_ASFIS_Foreign_Key_Relationship_With_Normalized_FieldNames()
        {
            // Arrange
            var wormsVocab = new WoRMSSpeciesVocabulary();
            wormsVocab.Load();

            // Act
            var result = _inferrer.AnalyzeVocabulary(wormsVocab);

            // Assert (use normalized source field name)
            string wormsFaoField = FieldPolicy.ForSchema("FAO_Code"); // => "fao-code"

            var candidate = FindFkCandidate(result.ForeignKeyCandidates, wormsFaoField, "asfis");
            candidate.Should().NotBeNull("Should identify FAO_Code as FK to ASFIS");
            candidate!.Score.Should().BeGreaterThan(10);
            candidate.Confidence.Should().BeGreaterThan(0.3);
        }

        // --- Helper methods (avoid LINQ where practical for legibility) ---

        private static Inference.FieldInferenceInfo? FindField(IEnumerable<Inference.FieldInferenceInfo> fields, string normalizedName)
        {
            foreach (var f in fields)
            {
                if (string.Equals(f.FieldName, normalizedName, StringComparison.Ordinal))
                    return f;
            }
            return null;
        }

        private static Inference.ForeignKeyMatchResult? FindFkCandidate(IEnumerable<Inference.ForeignKeyMatchResult> candidates, string sourceFieldNormalized, string targetVocabSubstringNormalized)
        {
            var targetNeedle = FieldPolicy.ForSchema(targetVocabSubstringNormalized);

            foreach (var c in candidates)
            {
                if (!string.Equals(FieldPolicy.ForSchema(c.SourceField), sourceFieldNormalized, StringComparison.Ordinal))
                    continue;

                var tv = FieldPolicy.ForSchema(c.TargetVocabulary);
                if (tv.Contains(targetNeedle, StringComparison.Ordinal))
                    return c;
            }
            return null;
        }
    }
}
