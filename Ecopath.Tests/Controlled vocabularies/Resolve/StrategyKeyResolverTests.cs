using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Resolve.Tests
{
    public class StrategyKeyResolverTests
    {
        public StrategyKeyResolverTests()
        {
        }

        [Fact]
        public void FindAllMatches_ReturnsNothing()
        {
            // Arrange
            List<EwEMapping> m_mappings = new();
            KeyFieldDescriptorRegistry m_keyFieldDescriptors = new();

            m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:juvenile", KeyDomain.Species, 22));       // Mullet (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:adult", KeyDomain.Species, 23));          // Mullet (a)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:HKE; stage=dwc:juvenile", KeyDomain.Species, 26));       // European Hake (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:HKE; stage=dwc:adult", KeyDomain.Species, 27));          // European Hake (a)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:ANE; stage=dwc:juvenile", KeyDomain.Species, 39));       // Anchovy (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:ANE; stage=dwc:adult", KeyDomain.Species, 40));          // Anchovy (a)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:PIL; stage=dwc:juvenile", KeyDomain.Species, 41));       // Sardine (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:PIL; stage=dwc:adult", KeyDomain.Species, 42));          // Sardine (a)


            // Register the different species fields that the application may be interested in
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.SpeciesCode, KeyDomain.Species, KeyPurpose.Species, true, 10));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Stage, KeyDomain.Species, KeyPurpose.Lifestage, false, 3));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Length, KeyDomain.Species, KeyPurpose.Length, false, 3));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Age, KeyDomain.Species, KeyPurpose.Age, false, 3));

            var resolver = new StrategyKeyResolver(m_mappings, m_keyFieldDescriptors.GetAll(KeyDomain.Species));

            // Act
            MultiLevelKey key = MultiLevelKey.FromPairs([(FishingFields.GearCode, "Bogus"), (MarketFields.MarketCode, "EVen more bogus")], KeyDomain.Species);
            var match = resolver.FindAllMatches(key);

            // Assert
            match.Count().Should().Be(0, "because I have no clue");
        }

        [Fact]
        public void FindAllMatches_ReturnsMatch_WhenKeyMatchesMapping()
        {
            // Arrange
            List<EwEMapping> m_mappings = new();
            KeyFieldDescriptorRegistry m_keyFieldDescriptors = new();

            m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:juvenile", KeyDomain.Species, 22)); // Mullet (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:adult", KeyDomain.Species, 23));    // Mullet (a)

            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.SpeciesCode, KeyDomain.Species, KeyPurpose.Species, true, 10));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Stage, KeyDomain.Species, KeyPurpose.Lifestage, false, 3));

            var resolver = new StrategyKeyResolver(m_mappings, m_keyFieldDescriptors.GetAll(KeyDomain.Species));

            // Act
            MultiLevelKey key = MultiLevelKey.FromPairs([(SpeciesFields.SpeciesCode, "ASFIS:MUR"), (SpeciesFields.Lifestage, "surimi:juvenile")], KeyDomain.Species);
            var matches = resolver.FindAllMatches(key);

            // Assert
            matches.Should().NotBeEmpty("because the key matches a mapping");
            matches.First().MatchedKey.Domain.Should().Be(KeyDomain.Species);
        }

        // Example of a theory test with inline data
        //[Theory]
        //[InlineData(1, 2, 3)]
        //[InlineData(-4, -6, -10)]
        //[InlineData(-2, 2, 0)]
        //[InlineData(int.MinValue, -1, int.MaxValue)]
        //public void CanAddTheory(int value1, int value2, int expected)
        //{
        //    var calculator = new Calculator();

        //    var result = calculator.Add(value1, value2);

        //    Assert.Equal(expected, result);
        //}
    }
}