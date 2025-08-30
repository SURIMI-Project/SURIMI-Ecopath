using ControlledVocabularies.Common;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;
using ControlledVocabularies.Vocabularies.Tests;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.ForeignKeys.Tests
{
    public class ForeignKeyAndRegistryTests
    {
        public ForeignKeyAndRegistryTests()
        {
            // fresh registry per test class
            GlobalServiceLocator.Register(new VocabularyRegistry());
        }

        [Fact]
        public void SetFK_Sets_And_Is_Idempotent()
        {
            var src = new TestSourceWithField("speciescode", KeyDomain.Species, KeyPurpose.Species);
            var tgt = new ASFISSpeciesCodeVocabulary(); tgt.Load().Should().BeTrue();

            src.SetForeignKey("speciescode", tgt, "Alpha3_Code").Should().BeTrue();
            src.SetForeignKey("speciescode", tgt, "Alpha3_Code").Should().BeTrue(); // idempotent

            src.GetForeignKeyFieldNames.Count().Should().Be(1);   

            var d = ((ControlledVocabularyBase)src).GetKeyFieldDescriptor("speciescode")!;
            d.ForeignKey!.TargetVocabulary.Should().Be("asfis");
            d.ForeignKey!.TargetField.Should().Be("alpha3-code");
        }

        [Fact]
        public void SetFK_Replaces_Previous()
        {
            var src = new TestSourceWithField("fieldx", KeyDomain.FleetSegment, KeyPurpose.Gear);
            var t1 = new ISSCFGGearCodeVocabulary(); t1.Load().Should().BeTrue();
            var t2 = new SURIMILifestageVocabulary(); t2.Load().Should().BeTrue();

            src.SetForeignKey("fieldx", t1, "GEAR_CODE").Should().BeTrue();
            src.SetForeignKey("fieldx", t2, "id").Should().BeTrue(); // replaces t1

            src.GetForeignKeyFieldNames.Count().Should().Be(1);

            var fk = ((ControlledVocabularyBase)src).GetKeyFieldDescriptor("fieldx")!.ForeignKey!;
            fk.TargetVocabulary.Should().Be("surimi.lifestage"); // Namespace dot preserved
            fk.TargetField.Should().Be("id");
        }

        [Fact]
        public void RemoveFK_ByField_And_ByTarget()
        {
            var src = new TestSourceWithField("flag", KeyDomain.FleetSegment, KeyPurpose.Country);
            var iso = new ISO3166CountryCodeVocabulary(); iso.Load().Should().BeTrue();

            src.SetForeignKey("flag", iso, "alpha-3").Should().BeTrue();

            // wrong target name := no remove
            src.RemoveForeignKey("flag", "ASFIS").Should().BeFalse();

            // correct target name := remove
            src.RemoveForeignKey("flag", "ISO-3166").Should().BeTrue();

            // already gone := false
            src.RemoveForeignKey("flag").Should().BeFalse();

            src.GetForeignKeyFieldNames.Count().Should().Be(0);
        }

        [Fact]
        public void FK_FastPath_Resolves_To_Target_With_Score100_And_Names()
        {
            var registry = GlobalServiceLocator.Get<VocabularyRegistry>()!;
            var target = new TestSpeciesVocabulary();        // has code "COD"
            var source = new TestSourceWithFKVocabulary();   // FK := TestSpeciesVocabulary.code

            registry.Register(target);
            registry.Register(source);

            var record = MultiLevelKey.FromPairs([(SpeciesFields.SpeciesCode, "COD")], KeyDomain.Species, registry: null, strict: false);

            var matcher = new GenericVocabularyMatcher(); // should pickup registry via service locator
            var result = matcher.Match(record, source, target, minscore: 80);

            result.Should().NotBeNull();
            result.Score.Should().Be(100);
            result.SourceVocabulary.Should().Be(FieldPolicy.ForSchema("TestSource"));
            result.TargetVocabulary.Should().Be(FieldPolicy.ForSchema("TestSpecies"));
            result.SourceField.Should().Be("speciescode");
            result.TargetField.Should().Be("code");
            result.TargetFieldValue.Should().Be("COD");
            result.StrategyUsed.Should().Be(MatchStrategy.Exact);

            registry.Unregister(target);
            registry.Unregister(source);
        }

        [Fact]
        public void Auto_Target_Picks_Best_Compatible_Vocabulary()
        {
            var registry = GlobalServiceLocator.Get<VocabularyRegistry>()!;
            var speciesA = new TestSpeciesVocabulary(); // contains COD
            var speciesB = new TestSpeciesVocabulary("AltSpecies", new[] { ("AAA", "alpha") }); // no COD
            var source = new TestSourceWithFKVocabulary();

            registry.Register(speciesA);
            registry.Register(speciesB);
            registry.Register(source);

            var record = MultiLevelKey.FromPairs([("speciescode", "COD")], KeyDomain.Species, registry: null, strict: false);

            var matcher = new GenericVocabularyMatcher();
            var result = matcher.Match(record, source, vocabB: null, minscore: 80);

            result.Score.Should().BeGreaterOrEqualTo(80);
            result.TargetVocabulary.Should().Be("testspecies"); // the only target that can resolve COD
            result.SourceVocabulary.Should().Be("testsource");
        }

        [Fact]
        public void Wrong_Target_Ignores_FK_And_Yields_NoMatch()
        {
            var registry = GlobalServiceLocator.Get<VocabularyRegistry>()!;
            var lifestage = new TestLifestageVocabulary();
            var source = new TestSourceWithFKVocabulary();

            registry.Register(lifestage);
            registry.Register(source);

            var record = MultiLevelKey.FromPairs([("speciescode", "COD")], KeyDomain.Species, registry: null, strict: false);

            var matcher = new GenericVocabularyMatcher();
            var result = matcher.Match(record, source, lifestage, minscore: 80);

            result.Score.Should().Be(0);
            // names should still be populated per requirement
            result.SourceVocabulary.Should().Be("TestSource");
            result.TargetVocabulary.Should().Be("TestLifestage");
        }

        [Fact]
        public void Name_Overload_Resolves_Equivalently()
        {
            var registry = GlobalServiceLocator.Get<VocabularyRegistry>()!;
            var species = new TestSpeciesVocabulary();
            var source = new TestSourceWithFKVocabulary();

            registry.Register(species);
            registry.Register(source);

            var record = MultiLevelKey.FromPairs([("speciescode", "COD")], KeyDomain.Species, registry: null, strict: false);

            var matcher = new GenericVocabularyMatcher();
            var byObjects = matcher.Match(record, registry.Get("TestSource")!, registry.Get("TestSpecies")!, 80);
            var byNames = matcher.Match(record, "TestSource", "TestSpecies", 80);

            byObjects.Score.Should().Be(byNames.Score);
            byObjects.TargetVocabulary.Should().Be(byNames.TargetVocabulary);
            byObjects.SourceVocabulary.Should().Be(byNames.SourceVocabulary);
        }

        [Fact]
        public void Indexer_Infers_Code_Uri_Label()
        {
            var v = new InMemoryVocabulary("T", KeyDomain.Species, KeyPurpose.Species);
            v.AddField("code", KeyPurpose.Species, FieldKind.Code, true, strategy: MatchStrategy.Exact);
            v.AddField("name", KeyPurpose.Species, FieldKind.Label, true, strategy: MatchStrategy.Fuzzy);
            v.AddField("link", KeyPurpose.Species, FieldKind.Uri, strategy:MatchStrategy.Exact);
            v.AddField("secret", KeyPurpose.Species, FieldKind.Unknown, strategy:MatchStrategy.Exact);

            v.AddRow(("code", "ESP"), ("name", "European Union"), ("link", "https://example.org/x"), ("secret", "777"));
            v.Load().Should().BeTrue();

            v.GetKeyFieldDescriptor("code")!.Kind.Should().Be(FieldKind.Code);
            v.GetKeyFieldDescriptor("name")!.Kind.Should().Be(FieldKind.Label);
            v.GetKeyFieldDescriptor("link")!.Kind.Should().Be(FieldKind.Uri);
            v.GetKeyFieldDescriptor("link")!.Kind.Should().Be(FieldKind.Uri);
        }

        [Fact]
        public void Exact_Compare_Uses_Code_Normalization()
        {
            var src = MultiLevelKey.FromPairs([("code", "esp")], KeyDomain.FleetSegment, strict: false);
            var tgtRow = MultiLevelKey.FromPairs([("code", "ESP")], KeyDomain.FleetSegment, strict: false);
            var descr = new KeyFieldDescriptor("code", KeyDomain.FleetSegment, KeyPurpose.Gear, FieldKind.Code, isRequired: true, weight: 1, strategy: MatchStrategy.Exact) { Kind = FieldKind.Code };

            var r = new ControlledVocabularies.Resolve.StrategyKeyResolver([tgtRow], [descr]);
            var best = r.FindBestMatch(src);
            best!.Score.Should().Be(100);
        }
    }
}