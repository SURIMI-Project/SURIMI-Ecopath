using ControlledVocabularies.Core;
using ControlledVocabularies.Vocabularies.Tests;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Descriptors.Tests
{
    public class KeyFieldIndexerTests
    {
        [Fact]
        public void Indexer_Infers_Kind_And_Strategy_For_Code()
        {
            var v = new TestSourceWithFields("T", KeyDomain.Species, KeyPurpose.Species,
                (SpeciesFields.SpeciesCode, true, MatchStrategy.None));

            v.AddRow((SpeciesFields.SpeciesCode, "ESP"))
             .AddRow((SpeciesFields.SpeciesCode, "FRA"))
             .AddRow((SpeciesFields.SpeciesCode, "NOR"));

            v.Load().Should().BeTrue();

            var d = v.GetKeyFieldDescriptor(SpeciesFields.SpeciesCode)!;
            d.Kind.Should().Be(FieldKind.Code);
            d.Strategy.HasFlag(MatchStrategy.Exact).Should().BeTrue();
        }

        [Fact]
        public void Indexer_Infers_Label_With_Fuzzy_And_TokenOverlap()
        {
            var v = new TestSourceWithFields("T", KeyDomain.Species, KeyPurpose.Species,
                ("name", true, MatchStrategy.None));

            foreach (var s in Enumerable.Range(0, 150))
                v.AddRow(("name", $"Drifting longlines variant {s}"));

            v.Load().Should().BeTrue();

            var d = v.GetKeyFieldDescriptor("name")!;
            d.Kind.Should().Be(FieldKind.Label);
            d.Strategy.HasFlag(MatchStrategy.Fuzzy).Should().BeTrue();
            d.Strategy.HasFlag(MatchStrategy.TokenOverlap).Should().BeTrue();
        }

        [Fact]
        public void Indexer_Infers_Uri_As_NoMatchable()
        {
            var v = new TestSourceWithFields("T", KeyDomain.Species, KeyPurpose.Species,
                ("link", false, MatchStrategy.None));

            v.AddRow(("link", "https://example.org/A"))
             .AddRow(("link", "https://example.org/B"));

            v.Load().Should().BeTrue();

            var d = v.GetKeyFieldDescriptor("link")!;
            d.Kind.Should().Be(FieldKind.Uri);
            d.Strategy.Should().Be(MatchStrategy.None);
        }
    }
}

