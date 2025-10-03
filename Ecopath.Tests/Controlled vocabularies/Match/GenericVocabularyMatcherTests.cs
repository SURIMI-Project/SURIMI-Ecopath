using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Match
{
    public class GenericVocabularyMatcherTests
    {
        public GenericVocabularyMatcherTests()
        {
        }

        [Fact]
        public void TestMatchCompatibleVocabularies()
        {
            SURIMILifestageVocabulary v1 = new();
            NERCLifeStageVocabulary v2 = new();

            MatchHelpers.CanMatch(v1, v2).Should().BeTrue();
        }

        [Fact]
        public void TestMatchIncompatibleVocabularies()
        {
            SURIMILifestageVocabulary v1 = new();
            ASFISSpeciesCodeVocabulary v2 = new();

            MatchHelpers.CanMatch(v1, v2).Should().BeFalse();
        }

        [Fact]
        public void TestFindMatches()
        {
            SURIMILifestageVocabulary v1 = new();
            v1.Load().Should().BeTrue();

            NERCLifeStageVocabulary v2 = new();
            v2.Load().Should().BeTrue();

            GenericVocabularyMatcher m = new();
            MultiLevelKey key = MultiLevelKey.FromPairs([(SpeciesFields.Lifestage, "juvenile")], KeyDomain.Species, strict:false);

            // This is a bloody big deal: a SURIMI lifestage code can be matched to a record from a totally independent vocabulary
            var result = m.Match(key, v1, v2);
            result.Score.Should().BeGreaterThan(70);
        }
    }
}