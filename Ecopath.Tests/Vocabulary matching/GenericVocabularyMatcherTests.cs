using FluentAssertions;
using Xunit;

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
        GenericVocabularyMatcher m = new();

        m.CanMatch(v1, v2 ).Should().BeTrue();
    }

    [Fact]
    public void TestMatchIncompatibleVocabularies()
    {
        SURIMILifestageVocabulary v1 = new();
        ASFISSpeciesVocabulary v2 = new();
        GenericVocabularyMatcher m = new();

        m.CanMatch(v1, v2).Should().BeFalse();
    }

    [Fact]
    public void TestFindMatches()
    {
        SURIMILifestageVocabulary v1 = new();
        v1.Load().Should().BeTrue();

        NERCLifeStageVocabulary v2 = new();
        v2.Load().Should().BeTrue();

        GenericVocabularyMatcher m = new();
        MultiLevelKey key = new();
        key.SetField(SpeciesFields.Lifestage, "juvenile");

        // This is a bloody big deal: a SURIMI lifestage code can be matched to a record of a totally independent vocabulary
        var result = m.Match(key, v1, v2);
        result.Score.Should().BeGreaterThan(70);
    }
}
