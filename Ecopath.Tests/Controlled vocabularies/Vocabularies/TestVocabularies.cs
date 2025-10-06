using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Vocabularies.Tests
{
    internal sealed class TestSpeciesVocabulary : ControlledVocabularyBase
    {
        private readonly string _name;
        private readonly (string code, string label)[] _rows;
        private readonly IFieldInferenceOrchestrator _orchestrator;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly IVocabularyMatcher m_vocabularyMatcher;
        private IForeignKeyResolver m_foreignKeyResolver;

        public TestSpeciesVocabulary(IFieldInferenceOrchestrator fieldInferenceOrchestrator, string name = "TestSpecies",
                                     System.Collections.Generic.IEnumerable<(string code, string label)>? rows = null) : base(fieldInferenceOrchestrator)
        {
            _name = name;
            _rows = (rows ?? new[] { ("COD", "Gadus morhua") }).ToArray();

            var m_vocabularyRegistry = new VocabularyRegistry();
            var keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            m_foreignKeyResolver = new ForeignKeyResolver(m_vocabularyRegistry, m_keyFieldDescriptorRegistry);

            m_vocabularyMatcher = new GenericVocabularyMatcher(m_vocabularyRegistry, m_foreignKeyResolver, m_keyFieldDescriptorRegistry);
            _orchestrator = new FieldInferenceOrchestrator(m_vocabularyRegistry, new KeyFieldDescriptorRegistry(), m_vocabularyMatcher);

            Load(keyFieldDescriptorIndexer);
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
        public TestSourceWithFKVocabulary(IFieldInferenceOrchestrator m_fieldInferenceOrchestrator) : base (m_fieldInferenceOrchestrator)
        {
            var registry = new VocabularyRegistry();
            var keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            Load(keyFieldDescriptorIndexer); 
        }

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
        public TestLifestageVocabulary(IFieldInferenceOrchestrator fieldInferenceOrchestrator) : base(fieldInferenceOrchestrator)
        {
            var registry = new VocabularyRegistry();
            var keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            Load(keyFieldDescriptorIndexer); 
        }

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

            var registry = new VocabularyRegistry();
            var keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            Load(keyFieldDescriptorIndexer); // create descriptors immediately
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