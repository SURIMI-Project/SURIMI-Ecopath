using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Vocabularies.Tests
{
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
}