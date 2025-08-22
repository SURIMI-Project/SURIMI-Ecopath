namespace ControlledVocabularies.Core
{
    /// <summary>
    /// Helper class, mediates between a multi-level field, its vocabulary and its value.
    /// </summary>
    /// <todo>Make Value/Vocabulary init-only or enforce freeze via IFreezable to prevent mutation in published records.</todo>
    /// <todo>Document ToString(includeVocabulary) semantics and ensure culture-invariant formatting.</todo>

    public class MultiLevelKeyField
    {
        public MultiLevelKeyField(string value, string vocabulary)
        {
            System.Diagnostics.Debug.Assert(value.IndexOf(':') == -1);
            Value = value;
            Vocabulary = vocabulary;
        }
        public string Value { get; set; }
        public string Vocabulary { get; set; }

        public static MultiLevelKeyField? FromString(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            int iSep = value.IndexOf(':');
            if (iSep == -1)
                return new MultiLevelKeyField(value, string.Empty);
            return new MultiLevelKeyField(value.Substring(iSep + 1), value.Substring(0, iSep));
        }

        public string ToString(bool includeVocabulary = true)
        {
            if (string.IsNullOrWhiteSpace(this.Vocabulary) | !includeVocabulary)
                return this.Value;
            return this.Vocabulary + ":" + this.Value;
        }

        public override string ToString()
        {
            return ToString(true);
        }
    }
}