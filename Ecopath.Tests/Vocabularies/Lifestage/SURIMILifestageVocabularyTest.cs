using FluentAssertions;
using Xunit;

public class SURIMILifestageVocabularyTest
{
    public SURIMILifestageVocabularyTest()
    {
    }

    [Fact]
    public void TestMatching()
    {
        SURIMILifestageVocabulary v1 = new();
        v1.Load().Should().BeTrue();

        v1.MatchLifestage("juvenile").score.Should().BeGreaterThan(50, "Accept perfect match");
    }
}
