using ControlledVocabularies.Core;
using ControlledVocabularies.ForeignKeys;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;
using Ecopath.Services;
using FluentAssertions;
using Xunit;

public class ForeignKeyAndRegistrySmokeTests
{
    public ForeignKeyAndRegistrySmokeTests()
    {
        // fresh registry per test class
        GlobalServiceLocator.Register(new VocabularyRegistry());
    }

    [Fact]
    public void FK_FastPath_Resolves_To_Target_With_Score100_And_Names()
    {
        var registry = GlobalServiceLocator.Get<VocabularyRegistry>()!;
        var target = new TestSpeciesVocabulary();        // has code "COD"
        var source = new TestSourceWithFKVocabulary();   // FK → TestSpeciesVocabulary.code

        registry.Register(target);
        registry.Register(source);

        var record = MultiLevelKey.FromPairs([(SpeciesFields.SpeciesCode, "COD")], KeyDomain.Species, registry: null, strict: false
        );

        var matcher = new GenericVocabularyMatcher(); // should pickup registry via service locator
        var result = matcher.Match(record, source, target, minscore: 80);

        result.Should().NotBeNull();
        result.Score.Should().Be(100);
        result.SourceVocabulary.Should().Be("TestSource");
        result.TargetVocabulary.Should().Be("TestSpecies");
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

        var record = MultiLevelKey.FromPairs(
            new[] { ("speciescode", "COD") }, KeyDomain.Species, registry: null, strict: false
        );

        var matcher = new GenericVocabularyMatcher();
        var result = matcher.Match(record, source, vocabB: null, minscore: 80);

        result.Score.Should().BeGreaterOrEqualTo(80);
        result.TargetVocabulary.Should().Be("TestSpecies"); // the only target that can resolve COD
        result.SourceVocabulary.Should().Be("TestSource");
    }

    [Fact]
    public void Wrong_Target_Ignores_FK_And_Yields_NoMatch()
    {
        var registry = GlobalServiceLocator.Get<VocabularyRegistry>()!;
        var lifestage = new TestLifestageVocabulary();
        var source = new TestSourceWithFKVocabulary();

        registry.Register(lifestage);
        registry.Register(source);

        var record = MultiLevelKey.FromPairs(
            new[] { ("speciescode", "COD") }, KeyDomain.Species, registry: null, strict: false
        );

        var matcher = new GenericVocabularyMatcher();
        var result = matcher.Match(record, source, lifestage, minscore: 80);

        result.Score.Should().Be(0);
        // names should still be populated per your requirement
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

        var record = MultiLevelKey.FromPairs(
            new[] { ("speciescode", "COD") }, KeyDomain.Species, registry: null, strict: false
        );

        var matcher = new GenericVocabularyMatcher();
        var byObjects = matcher.Match(record, registry.Get("TestSource")!, registry.Get("TestSpecies")!, 80);
        var byNames = matcher.Match(record, "TestSource", "TestSpecies", 80);

        byObjects.Score.Should().Be(byNames.Score);
        byObjects.TargetVocabulary.Should().Be(byNames.TargetVocabulary);
        byObjects.SourceVocabulary.Should().Be(byNames.SourceVocabulary);
    }

    // ---------- tiny in-memory vocabs for tests ----------

    private sealed class TestSpeciesVocabulary : ControlledVocabularyBase
    {
        private readonly string _name;
        private readonly (string code, string label)[] _rows;

        public TestSpeciesVocabulary(string name = "TestSpecies",
                                     System.Collections.Generic.IEnumerable<(string code, string label)>? rows = null)
        {
            _name = name;
            _rows = (rows ?? new[] { ("COD", "Gadus morhua") }).ToArray();
            Load();
        }

        public override string VocabularyName => _name;
        public override string CodeFieldName => "code";
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Species;

        protected override bool LoadFromSource()
        {
            AddField("code", Domain, Purpose, isRequired: true, weight: 1, strategy: MatchStrategy.Exact);
            AddField("label", Domain, Purpose, isRequired: false, weight: 1, strategy: MatchStrategy.Exact | MatchStrategy.Fuzzy);

            foreach (var (code, label) in _rows)
            {
                var r = Table.NewRow();
                r["code"] = code;
                r["label"] = label;
                Table.Rows.Add(r);
            }
            return true;
        }
    }

    private sealed class TestSourceWithFKVocabulary : ControlledVocabularyBase
    {
        public TestSourceWithFKVocabulary() { Load(); }

        public override string VocabularyName => "TestSource";
        public override string CodeFieldName => "id"; // arbitrary
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Species;

        protected override bool LoadFromSource()
        {
            if (!Table.Columns.Contains("id")) Table.Columns.Add("id", typeof(string));
            if (!Table.Columns.Contains("speciescode")) Table.Columns.Add("speciescode", typeof(string));

            // build FK descriptor on field "speciescode"
            var col = Table.Columns["speciescode"]!;
            var descr = new ControlledVocabularyBase.DataTableKeyFieldDescriptor(col, Domain, Purpose,
                           isRequired: true, weight: 1, strategy: MatchStrategy.Exact)
            {
                ForeignKey = new ForeignKeySpec
                {
                    TargetVocabulary = "TestSpecies",
                    TargetField = "code",
                    TargetDomain = KeyDomain.Species,
                    TargetPurpose = KeyPurpose.Species,
                    Strict = true
                }
            };

            // register descriptors with base map
            m_descriptors["speciescode"] = descr;

            var idCol = Table.Columns["id"]!;
            m_descriptors["id"] = new ControlledVocabularyBase.DataTableKeyFieldDescriptor(idCol, Domain, Purpose,
                isRequired: false, weight: 1, strategy: MatchStrategy.Exact);

            return true;
        }
    }

    private sealed class TestLifestageVocabulary : ControlledVocabularyBase
    {
        public TestLifestageVocabulary() { Load(); }

        public override string VocabularyName => "TestLifestage";
        public override string CodeFieldName => "id";
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Lifestage;

        protected override bool LoadFromSource()
        {
            AddField("id", Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField("label", Domain, Purpose, true, 1, MatchStrategy.Exact | MatchStrategy.Fuzzy);

            var r1 = Table.NewRow(); r1["id"] = "juvenile"; r1["label"] = "young juvenile"; Table.Rows.Add(r1);
            var r2 = Table.NewRow(); r2["id"] = "adult"; r2["label"] = "adult"; Table.Rows.Add(r2);
            return true;
        }
    }
}
