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
    }
}
