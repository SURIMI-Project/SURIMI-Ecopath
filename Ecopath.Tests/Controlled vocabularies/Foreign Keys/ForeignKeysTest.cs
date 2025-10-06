using ControlledVocabularies.Vocabularies.Tests;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Resolve;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies;
using Eii.ControlledVocabularies.Vocabularies.Country;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace ControlledVocabularies.ForeignKeys.Tests
{
    public class ForeignKeyAndRegistryTests
    {
        private IVocabularyRegistry m_vocabularyRegistry;
        private IKeyFieldDescriptorIndexer _keyFieldDescriptorIndexer;
        private GenericVocabularyMatcher m_matcher;                 // TODO: Why not IVocabularyMatcher??
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;


        public ForeignKeyAndRegistryTests()
        {
            // fresh registry per test class
            m_vocabularyRegistry = new VocabularyRegistry();
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
            _keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            var foreignKeyResolver = new ForeignKeyResolver(m_vocabularyRegistry, m_keyFieldDescriptorRegistry);
            var vocabularyMatcher = new GenericVocabularyMatcher(m_vocabularyRegistry, foreignKeyResolver, m_keyFieldDescriptorRegistry);
            m_matcher = new GenericVocabularyMatcher(m_vocabularyRegistry, foreignKeyResolver, m_keyFieldDescriptorRegistry);
            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_vocabularyRegistry, m_keyFieldDescriptorRegistry, vocabularyMatcher);
        }

        [Fact]
        public void SetFK_Sets_And_Is_Idempotent()
        {
            var src = new TestSourceWithField("speciescode", KeyDomain.Species, KeyPurpose.Species);
            var tgt = new ASFISSpeciesCodeVocabulary(m_fieldInferenceOrchestrator); tgt.Load(_keyFieldDescriptorIndexer).Should().BeTrue();

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
            var t1 = new ISSCFGGearCodeVocabulary(m_fieldInferenceOrchestrator); t1.Load(_keyFieldDescriptorIndexer).Should().BeTrue();
            var t2 = new SURIMILifestageVocabulary(m_fieldInferenceOrchestrator); t2.Load(_keyFieldDescriptorIndexer).Should().BeTrue();

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
            var iso = new ISO3166CountryCodeVocabulary(m_fieldInferenceOrchestrator); iso.Load(_keyFieldDescriptorIndexer).Should().BeTrue();

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
            var target = new TestSpeciesVocabulary(m_fieldInferenceOrchestrator);        // has code "COD"
            var source = new TestSourceWithFKVocabulary(m_fieldInferenceOrchestrator);   // FK := TestSpeciesVocabulary.code

            m_vocabularyRegistry.Register(target, _keyFieldDescriptorIndexer);
            m_vocabularyRegistry.Register(source, _keyFieldDescriptorIndexer);

            var record = MultiLevelKey.FromPairs([(SpeciesFields.SpeciesCode, "COD")], KeyDomain.Species, registry: null, strict: false);

            var result = m_matcher.Match(record, source, target, minscore: 80);

            result.Should().NotBeNull();
            result.Score.Should().Be(100);
            result.SourceVocabulary.Should().Be(FieldPolicy.ForSchema("TestSource"));
            result.TargetVocabulary.Should().Be(FieldPolicy.ForSchema("TestSpecies"));
            result.SourceField.Should().Be("speciescode");
            result.TargetField.Should().Be("code");
            result.TargetFieldValue.Should().Be("COD");
            result.StrategyUsed.Should().Be(MatchStrategy.Exact);

            m_vocabularyRegistry.Unregister(target);
            m_vocabularyRegistry.Unregister(source);
        }

        [Fact]
        public void Auto_Target_Picks_Best_Compatible_Vocabulary()
        {
            var speciesA = new TestSpeciesVocabulary(m_fieldInferenceOrchestrator); // contains COD
            var speciesB = new TestSpeciesVocabulary(m_fieldInferenceOrchestrator, "AltSpecies", new[] { ("AAA", "alpha") }); // no COD
            var source = new TestSourceWithFKVocabulary(m_fieldInferenceOrchestrator);

            m_vocabularyRegistry.Register(speciesA, _keyFieldDescriptorIndexer);
            m_vocabularyRegistry.Register(speciesB, _keyFieldDescriptorIndexer);
            m_vocabularyRegistry.Register(source, _keyFieldDescriptorIndexer);

            var record = MultiLevelKey.FromPairs([("speciescode", "COD")], KeyDomain.Species, registry: null, strict: false);

            var result = m_matcher.Match(record, source, vocabB: null, minscore: 80);

            result.Score.Should().BeGreaterOrEqualTo(80);
            result.TargetVocabulary.Should().Be("testspecies"); // the only target that can resolve COD
            result.SourceVocabulary.Should().Be("testsource");
        }

        [Fact]
        public void Wrong_Target_Ignores_FK_And_Yields_NoMatch()
        {
            var lifestage = new TestLifestageVocabulary(m_fieldInferenceOrchestrator);
            var source = new TestSourceWithFKVocabulary(m_fieldInferenceOrchestrator);

            m_vocabularyRegistry.Register(lifestage, _keyFieldDescriptorIndexer);
            m_vocabularyRegistry.Register(source, _keyFieldDescriptorIndexer);

            var record = MultiLevelKey.FromPairs([("speciescode", "COD")], KeyDomain.Species, registry: null, strict: false);

            var result = m_matcher.Match(record, source, lifestage, minscore: 80);

            result.Score.Should().Be(0);
            // names should still be populated per requirement
            result.SourceVocabulary.Should().Be("TestSource");
            result.TargetVocabulary.Should().Be("TestLifestage");
        }

        [Fact]
        public void Name_Overload_Resolves_Equivalently()
        {
            var species = new TestSpeciesVocabulary(m_fieldInferenceOrchestrator);
            var source = new TestSourceWithFKVocabulary(m_fieldInferenceOrchestrator);

            m_vocabularyRegistry.Register(species, _keyFieldDescriptorIndexer);
            m_vocabularyRegistry.Register(source, _keyFieldDescriptorIndexer);

            var record = MultiLevelKey.FromPairs([("speciescode", "COD")], KeyDomain.Species, registry: null, strict: false);

            var byObjects = m_matcher.Match(record, m_vocabularyRegistry.Get("TestSource")!, m_vocabularyRegistry.Get("TestSpecies")!, 80);
            var byNames = m_matcher.Match(record, "TestSource", "TestSpecies", 80);

            byObjects.Score.Should().Be(byNames.Score);
            byObjects.TargetVocabulary.Should().Be(byNames.TargetVocabulary);
            byObjects.SourceVocabulary.Should().Be(byNames.SourceVocabulary);
        }

        [Fact]
        public void Indexer_Infers_Code_Uri_Label()
        {
            var v = new InMemoryVocabulary("T", KeyDomain.Species, KeyPurpose.Species, m_fieldInferenceOrchestrator);
            v.AddField("code", KeyPurpose.Species, FieldKind.Code, true, strategy: MatchStrategy.Exact);
            v.AddField("name", KeyPurpose.Species, FieldKind.Label, true, strategy: MatchStrategy.Fuzzy);
            v.AddField("link", KeyPurpose.Species, FieldKind.Uri, strategy:MatchStrategy.Exact);
            v.AddField("secret", KeyPurpose.Species, FieldKind.Unknown, strategy:MatchStrategy.Exact);

            v.AddRow(("code", "ESP"), ("name", "European Union"), ("link", "https://example.org/x"), ("secret", "777"));
            v.Load(_keyFieldDescriptorIndexer).Should().BeTrue();

            v.GetKeyFieldDescriptor("code")!.Kind.Should().Be(FieldKind.Code);
            v.GetKeyFieldDescriptor("name")!.Kind.Should().Be(FieldKind.Label);
            v.GetKeyFieldDescriptor("link")!.Kind.Should().Be(FieldKind.Uri);
            v.GetKeyFieldDescriptor("link")!.Kind.Should().Be(FieldKind.Uri);
        }

        [Fact]
        public void Exact_Compare_Uses_Code_Normalization()
        {
            var src = MultiLevelKey.FromPairs([("code", "esp")], KeyDomain.FleetSegment, m_keyFieldDescriptorRegistry, strict: false);
            var tgtRow = MultiLevelKey.FromPairs([("code", "ESP")], KeyDomain.FleetSegment, m_keyFieldDescriptorRegistry, strict: false);
            var descr = new KeyFieldDescriptor("code", KeyDomain.FleetSegment, KeyPurpose.Gear, FieldKind.Code, isRequired: true, weight: 1, strategy: MatchStrategy.Exact) { Kind = FieldKind.Code };

            var r = new StrategyKeyResolver([tgtRow], [descr]);
            var best = r.FindBestMatch(src);
            best!.Score.Should().Be(100);
        }
    }
}