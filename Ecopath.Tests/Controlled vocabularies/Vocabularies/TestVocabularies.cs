using ControlledVocabularies.Core;
using ControlledVocabularies.ForeignKeys;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Vocabularies.Tests
{
    internal sealed class TestSpeciesVocabulary : ControlledVocabularyBase
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

    internal sealed class TestSourceWithFKVocabulary : ControlledVocabularyBase
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
            var descr = new ControlledVocabularyBase.DataTableKeyFieldDescriptor(col, Domain, Purpose, FieldKind.Code,
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
            m_descriptors["id"] = new ControlledVocabularyBase.DataTableKeyFieldDescriptor(idCol, Domain, Purpose, FieldKind.Code,
                isRequired: false, weight: 1, strategy: MatchStrategy.Exact);

            return true;
        }
    }

    internal sealed class TestLifestageVocabulary : ControlledVocabularyBase
    {
        public TestLifestageVocabulary() { Load(); }

        public override string VocabularyName => "TestLifestage";
        public override string CodeFieldName => "id";
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Lifestage;

        protected override bool LoadFromSource()
        {
            AddField("id", Domain, Purpose, FieldKind.Code, true, 1, MatchStrategy.Exact);
            AddField("label", Domain, Purpose, FieldKind.Label, true, 1, MatchStrategy.Exact | MatchStrategy.Fuzzy);

            var r1 = Table.NewRow(); r1["id"] = "juvenile"; r1["label"] = "young juvenile"; Table.Rows.Add(r1);
            var r2 = Table.NewRow(); r2["id"] = "adult"; r2["label"] = "adult"; Table.Rows.Add(r2);
            return true;
        }
    }

    internal sealed class TestSourceWithField : ControlledVocabularyBase
    {
        private readonly string m_vocabName;
        private readonly string m_fieldName;
        private readonly KeyDomain m_domain;
        private readonly KeyPurpose m_purpose;

        /// <param name="vocabName">Optional name; defaults to "TestSource".</param>
        public TestSourceWithField(string fieldName, KeyDomain domain, KeyPurpose purpose, string vocabName = "TestSource")
        {
            m_vocabName = vocabName;
            m_fieldName = fieldName;
            m_domain = domain;
            m_purpose = purpose;
            Load(); // create descriptors immediately
        }

        public override string VocabularyName => m_vocabName;
        public override KeyDomain Domain => m_domain;
        public override KeyPurpose Purpose => m_purpose;

        // Arbitrary code column to satisfy the base; we don't actually use rows in tests.
        public override string CodeFieldName => "id";

        protected override bool LoadFromSource()
        {
            // Minimal schema: dummy code column + the single test field
            AddField(CodeFieldName, Domain, Purpose, isRequired: false, weight: 1, strategy: MatchStrategy.Exact);
            AddField(m_fieldName, Domain, Purpose, isRequired: true, weight: 1, strategy: MatchStrategy.Exact);

            // No rows needed for FK/mapping tests
            return true;
        }
    }    
}