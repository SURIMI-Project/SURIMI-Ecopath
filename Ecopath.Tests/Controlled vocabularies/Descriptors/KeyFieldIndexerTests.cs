using ControlledVocabularies.Core;
using ControlledVocabularies.Vocabularies;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Descriptors.Tests
{
    public class KeyFieldIndexerTests
    {
        [Fact]
        public void Indexer_Infers_Kind_And_Strategy_For_Code()
        {
            var v = new InMemoryVocabulary("T", KeyDomain.Species, KeyPurpose.Species);
            v.AddField(SpeciesFields.SpeciesCode, KeyPurpose.Species, strategy: MatchStrategy.None);
            v.AddRow((SpeciesFields.SpeciesCode, "HKE"));
            v.AddRow((SpeciesFields.SpeciesCode, "GUP"));
            v.AddRow((SpeciesFields.SpeciesCode, "MUL"));
            v.Load().Should().BeTrue();

            var d = v.GetKeyFieldDescriptor(SpeciesFields.SpeciesCode)!;
            d.Kind.Should().Be(FieldKind.Code);
            d.Strategy.HasFlag(MatchStrategy.Exact).Should().BeTrue();
        }

        [Fact]
        public void Indexer_Infers_Label_With_Fuzzy_And_TokenOverlap()
        {
            var v = new InMemoryVocabulary("T", KeyDomain.Species, KeyPurpose.Species);
            v.AddField("name", KeyPurpose.NotSet, strategy: MatchStrategy.None);

            foreach (var s in Enumerable.Range(0, 150))
                v.AddRow(("name", $"Drifting longlines variant {s}"));

            v.Load().Should().BeTrue();

            var d = v.GetKeyFieldDescriptor("name")!;
            d.Kind.Should().Be(FieldKind.Label);
            d.Strategy.HasFlag(MatchStrategy.Keyword).Should().BeTrue(); // Too long for Fuzzy
            d.Strategy.HasFlag(MatchStrategy.TokenOverlap).Should().BeTrue();
        }

        [Fact]
        public void Indexer_Infers_Uri_As_NoMatchable()
        {
            var v = new InMemoryVocabulary("T", KeyDomain.Species, KeyPurpose.Species);
            v.AddField("link", KeyPurpose.Species, strategy:MatchStrategy.None);
            v.AddRow(("link", "https://example.org/A"));
            v.AddRow(("link", "https://example.org/B"));

            v.Load().Should().BeTrue();

            var d = v.GetKeyFieldDescriptor("link")!;
            d.Kind.Should().Be(FieldKind.Uri);
            d.Strategy.Should().Be(MatchStrategy.None);
        }
    }
}

