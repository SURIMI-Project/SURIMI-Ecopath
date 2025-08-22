using ControlledVocabularies.Core;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Vocabularies
{
    /// <summary>
    /// 
    /// </summary>
    public class InMemoryVocabulary : ControlledVocabularyBase
    {
        private readonly List<FieldDef> m_fields = new();
        private readonly List<(string field, string? value)[]> m_rows = new();

        private sealed record FieldDef( string Name, KeyPurpose Purpose, bool IsRequired, int Weight, MatchStrategy Strategy, FieldKind? Kind, bool IsCode);

        public InMemoryVocabulary(string vocabularyName, KeyDomain domain, KeyPurpose purpose)
            : base(vocabularyName, domain, purpose) { }

        public InMemoryVocabulary AddField( string name, KeyPurpose purpose, bool required = false, int weight = 0, MatchStrategy strategy = MatchStrategy.None, FieldKind? kind = null, bool isCode = false)
        {
            m_fields.Add(new FieldDef(FieldPolicy.ForSchema(name), purpose, required, weight, strategy, kind, isCode));
            return this;
        }

        public InMemoryVocabulary AddRow(params (string field, string? value)[] cells)
        {
            var normalized = new (string, string?)[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                normalized[i] = (FieldPolicy.ForSchema(cells[i].field), cells[i].value);
            m_rows.Add(normalized);
            return this;
        }

        protected override bool LoadFromSource()
        {
            // define columns/descriptors
            foreach (var f in m_fields)
            {
                var d = AddField(f.Name, Domain, f.Purpose, f.IsRequired, f.Weight, f.Strategy);
                if (f.Kind.HasValue) d.Kind = f.Kind.Value;                   // pin Kind if provided
                if (f.Strategy != MatchStrategy.None) d.Strategy = f.Strategy; // pin Strategy if provided
                if (f.IsCode) CodeFieldName = f.Name;
            }

            // rows
            foreach (var row in m_rows)
            {
                var dr = Table.NewRow();
                foreach (var (field, value) in row)
                    dr[field] = value ?? string.Empty;
                Table.Rows.Add(dr);
            }

            // choose a sensible code field if not explicitly pinned
            if (string.IsNullOrEmpty(CodeFieldName))
                CodeFieldName = m_fields.FirstOrDefault(f => f.IsRequired)?.Name ?? m_fields.First().Name;

            return true;
        }
    }

}
