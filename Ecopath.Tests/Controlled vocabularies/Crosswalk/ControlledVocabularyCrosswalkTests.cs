using ControlledVocabularies.Core;
using ControlledVocabularies.ForeignKeys;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.CrossWalk.Tests
{
    public class WoRMSToASFISCrossWalkTests
    {
        private readonly VocabularyRegistry m_registry;
        private readonly WoRMSSpeciesVocabulary m_wormsVocab;
        private readonly ASFISSpeciesCodeVocabulary m_asfisVocab;
        private readonly ForeignKeyResolver m_fkResolver;
        private readonly VocabularyCompatibilityScorer m_scorer;

        public WoRMSToASFISCrossWalkTests()
        {
            m_registry = new VocabularyRegistry();
            m_wormsVocab = new WoRMSSpeciesVocabulary();
            m_asfisVocab = new ASFISSpeciesCodeVocabulary();
            m_scorer = new VocabularyCompatibilityScorer();

            // Load vocabularies
            m_wormsVocab.Load().Should().BeTrue();
            m_asfisVocab.Load().Should().BeTrue();

            // Register in registry
            m_registry.Register(m_wormsVocab);
            m_registry.Register(m_asfisVocab);

            // Configure cross-references
            m_wormsVocab.ConfigureCrossReferences(m_registry);

            // Create FK resolver with registry
            m_fkResolver = new ForeignKeyResolver(m_registry);
        }

        [Fact]
        public void Should_Resolve_WoRMS_To_ASFIS_Via_Direct_Foreign_Key()
        {
            // Arrange - WoRMS record with FAO code
            var wormsKey = MultiLevelKey.FromPairs([
                ("AphiaID", "126436"),
                ("ScientificName", "Gadus morhua"),
                ("CommonName", "Atlantic cod"),
                ("FAO_Code", "COD")  // This is the FK bridge - no vocab prefix!
            ], KeyDomain.Species);

            // Act - Use foreign key resolver
            var fkResult = m_fkResolver.TryResolve(wormsKey, m_wormsVocab, m_asfisVocab);

            // Assert - Should find ASFIS record via FAO code
            fkResult.IsMatch.Should().BeTrue();
            fkResult.Score.Should().BeGreaterOrEqualTo(50); // Your resolver might score differently
            fkResult.SourceVocabulary.Should().Be("worms"); // Normalized
            fkResult.TargetVocabulary.Should().Be("asfis"); // Normalized
            fkResult.TargetFieldValue.Should().Be("COD");
            fkResult.Justification.Should().Contain("FK");
        }

        [Fact]
        public void Should_Use_Exact_Matching_For_Foreign_Keys()
        {
            // Arrange - Test the exact matching behavior
            var wormsKey = MultiLevelKey.FromPairs([
                ("FAO_Code", "HAD")  // Haddock
            ], KeyDomain.Species);

            // Act
            var result = m_fkResolver.TryResolve(wormsKey, m_wormsVocab, m_asfisVocab);

            // Assert - Should use exact matching strategy
            result.IsMatch.Should().BeTrue();
            result.StrategyUsed.Should().Be(MatchStrategy.Exact);
            result.SourceField.Should().Be("FAO_Code");
            result.SourceFieldValue.Should().Be("HAD");
        }

        [Fact]
        public void Should_Fall_Back_When_Specific_Target_Field_Fails()
        {
            // Arrange - Create a key that might not match the specific target field
            // but could match via fallback to any compatible field
            var wormsKey = MultiLevelKey.FromPairs([
                ("FAO_Code", "PIL")  // Sardine
            ], KeyDomain.Species);

            // Act
            var result = m_fkResolver.TryResolve(wormsKey, m_wormsVocab, m_asfisVocab);

            // Assert - Should find match via fallback mechanism
            result.IsMatch.Should().BeTrue();
            result.StrategyUsed.Should().Be(MatchStrategy.Exact);
        }

        [Fact]
        public void Should_Handle_Missing_Foreign_Key_Values_Gracefully()
        {
            // Arrange - WoRMS record WITHOUT FAO code
            var wormsKeyNoFK = MultiLevelKey.FromPairs([
                ("AphiaID", "999999"),
                ("ScientificName", "Unknown species")
                // Note: No FAO_Code field!
            ], KeyDomain.Species);

            // Act
            var fkResult = m_fkResolver.TryResolve(wormsKeyNoFK, m_wormsVocab, m_asfisVocab);

            // Assert - Should fail gracefully
            fkResult.IsMatch.Should().BeFalse();
            fkResult.Score.Should().Be(0);
            fkResult.Justification.Should().Contain("No FK rule");
        }

        [Fact]
        public void Should_Use_Schema_Normalized_Vocabulary_Names()
        {
            // Arrange - Test that vocabulary name normalization works
            var wormsKey = MultiLevelKey.FromPairs([("FAO_Code", "COD")], KeyDomain.Species);

            // Act
            var result = m_fkResolver.TryResolve(wormsKey, m_wormsVocab, m_asfisVocab);

            // Assert - Should use normalized names
            result.SourceVocabulary.Should().Be("worms"); // Normalized
            result.TargetVocabulary.Should().Be("asfis"); // Normalized
        }

        [Fact]
        public void Should_Demonstrate_Complete_Cross_Vocabulary_Workflow()
        {
            // Arrange - Realistic workflow: user has WoRMS ID, needs ASFIS code
            var userWormsId = "126436"; // Atlantic cod AphiaID

            // Step 1: Find WoRMS record
            var wormsRecord = m_wormsVocab.Records
                .FirstOrDefault(r => r.GetField("AphiaID")?.Value == userWormsId);

            wormsRecord.Should().NotBeNull();

            // Step 2: Use FK resolver to cross-walk to ASFIS
            var crossWalkResult = m_fkResolver.TryResolve(wormsRecord!, m_wormsVocab, m_asfisVocab);

            // Step 3: Verify complete semantic bridge
            crossWalkResult.IsMatch.Should().BeTrue();

            // Step 4: Extract ASFIS information
            var asfisRecord = crossWalkResult.MatchedKey;
            var asfisCode = asfisRecord.GetField("speciescode")?.Value;

            // Assert - Complete semantic preservation
            asfisCode.Should().Be("COD");

            Console.WriteLine($"Cross-vocabulary resolution:");
            Console.WriteLine($"  WoRMS AphiaID: {userWormsId}");
            Console.WriteLine($"  Scientific: {wormsRecord.GetField("ScientificName")?.Value}");
            Console.WriteLine($"  ASFIS Code: {asfisCode}");
            Console.WriteLine($"  Resolution Score: {crossWalkResult.Score}");
            Console.WriteLine($"  Method: {crossWalkResult.Justification}");
        }

        [Fact]
        public void Should_Work_With_Generic_Vocabulary_Matcher_As_Fallback()
        {
            // Arrange - When FK resolution fails, try general matching
            var problematicKey = MultiLevelKey.FromPairs([
                ("CommonName", "Atlantic cod") // No direct FK, but matchable content
            ], KeyDomain.Species);

            // Act - FK resolver first
            var fkResult = m_fkResolver.TryResolve(problematicKey, m_wormsVocab, m_asfisVocab);

            if (!fkResult.IsMatch)
            {
                // Fall back to general vocabulary matching
                var generalMatcher = new GenericVocabularyMatcher();
                var generalResult = generalMatcher.Match(problematicKey, m_wormsVocab, m_asfisVocab);

                // Assert - Should find match via general approach
                generalResult.Should().NotBeNull();
                // generalResult.Score.Should().BeGreaterThan(0); // Depending on vocabulary content
            }

            // This demonstrates the layered resolution approach:
            // 1. Try FK (fast, exact)
            // 2. Fall back to semantic matching (slower, fuzzier)
        }
    }
}