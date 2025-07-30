using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Xunit;
using FluentAssertions;


public class StaticKeyResolverTests
{
    public StaticKeyResolverTests()
    {
    }

    [Fact]
    public void FindAllMatchesReturnsNothing()
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
        m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.SpeciesCode, true, 10));
        m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.Stage, false, 3));
        m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.Length, false, 3));
        m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.Age, false, 3));


        var resolver = new StaticKeyResolver(m_mappings, m_keyFieldDescriptors.Get(KeyDomain.Species));

        // Act
        MultiLevelKey key = new();
        key.SetField("GearCode", "");
        key.SetField("MarketCode", "ESAQA");

        var match = resolver.FindAllMatches(key, KeyDomain.Species);

        // Assert
        match.Count().Should().Be(0, "because have no clue");
    }

    [Fact]
    public void FindAllMatches_ReturnsMatch_WhenKeyMatchesMapping()
    {
        // Arrange
        List<EwEMapping> m_mappings = new();
        KeyFieldDescriptorRegistry m_keyFieldDescriptors = new();

        m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:juvenile", KeyDomain.Species, 22)); // Mullet (j)
        m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:adult", KeyDomain.Species, 23));    // Mullet (a)

        m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.SpeciesCode, true, 10));
        m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.Stage, false, 3));

        var resolver = new StaticKeyResolver(m_mappings, m_keyFieldDescriptors.Get(KeyDomain.Species));

        // Act
        MultiLevelKey key = new();
        key.SetField("speciescode", "ASFIS:MUR");
        key.SetField("stage", "dwc:juvenile");

        var matches = resolver.FindAllMatches(key, KeyDomain.Species);

        // Assert
        matches.Should().NotBeEmpty("because the key matches a mapping");
        matches.First().Key.Domain.Should().Be(KeyDomain.Species);
    }
}
