using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Resolve;
using Eii.ControlledVocabularies.Utils;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Resolve.Tests
{
    public class StrategyKeyResolverTests
    {
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;

        public StrategyKeyResolverTests()
        {
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
        }

        [Fact]
        public void FindAllMatches_ReturnsNothing()
        {
            // Arrange
            List<EwEMapping> m_mappings = new();
            KeyFieldDescriptorRegistry m_keyFieldDescriptors = new();

            m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:juvenile", KeyDomain.Species, 22, m_keyFieldDescriptors));       // Mullet (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:adult", KeyDomain.Species, 23, m_keyFieldDescriptors));          // Mullet (a)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:HKE; stage=dwc:juvenile", KeyDomain.Species, 26, m_keyFieldDescriptors));       // European Hake (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:HKE; stage=dwc:adult", KeyDomain.Species, 27, m_keyFieldDescriptors));          // European Hake (a)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:ANE; stage=dwc:juvenile", KeyDomain.Species, 39, m_keyFieldDescriptors));       // Anchovy (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:ANE; stage=dwc:adult", KeyDomain.Species, 40, m_keyFieldDescriptors));          // Anchovy (a)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:PIL; stage=dwc:juvenile", KeyDomain.Species, 41, m_keyFieldDescriptors));       // Sardine (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:PIL; stage=dwc:adult", KeyDomain.Species, 42, m_keyFieldDescriptors));          // Sardine (a)


            // Register the different species fields that the application may be interested in
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.SpeciesCode, KeyDomain.Species, KeyPurpose.Species, FieldKind.Code, true, 10));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Stage, KeyDomain.Species, KeyPurpose.Lifestage, FieldKind.Label, false, 3));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Length, KeyDomain.Species, KeyPurpose.Length, FieldKind.Label, false, 3));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Age, KeyDomain.Species, KeyPurpose.Age, FieldKind.Label, false, 3));

            var resolver = new StrategyKeyResolver(m_mappings, m_keyFieldDescriptors.GetAll(KeyDomain.Species));

            // Act
            MultiLevelKey key = MultiLevelKey.FromPairs([(FishingFields.GearCode, "Bogus"), (MarketFields.MarketCode, "EVen more bogus")], KeyDomain.Species, m_keyFieldDescriptorRegistry);
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

            m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:juvenile", KeyDomain.Species, 22, m_keyFieldDescriptors)); // Mullet (j)
            m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:adult", KeyDomain.Species, 23, m_keyFieldDescriptors));    // Mullet (a)

            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.SpeciesCode, KeyDomain.Species, KeyPurpose.Species, FieldKind.Code, true, 10));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Stage, KeyDomain.Species, KeyPurpose.Lifestage, FieldKind.Label, false, 3));

            var resolver = new StrategyKeyResolver(m_mappings, m_keyFieldDescriptors.GetAll(KeyDomain.Species));

            // Act
            MultiLevelKey key = MultiLevelKey.FromPairs([(SpeciesFields.SpeciesCode, "ASFIS:MUR"), (SpeciesFields.Lifestage, "surimi:juvenile")], KeyDomain.Species, m_keyFieldDescriptorRegistry);
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