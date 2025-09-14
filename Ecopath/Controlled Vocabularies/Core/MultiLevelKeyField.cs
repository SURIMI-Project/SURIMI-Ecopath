using OpenTelemetry.Trace;

namespace ControlledVocabularies.Core
{
    /// <inheritdoc/>
    /// <todo>Make Value/Vocabulary init-only or enforce freeze via IFreezable to prevent mutation in published records.</todo>
    /// <todo>Document ToString(includeVocabulary) semantics and ensure culture-invariant formatting.</todo>
    public class MultiLevelKeyField : IMultiLevelKeyField
    {
        public MultiLevelKeyField(string value, string vocabulary)
        {
            // How about URI / URL / DOI?
            //System.Diagnostics.Debug.Assert(value.IndexOf(':') == -1);
            Value = value;
            Vocabulary = vocabulary;
        }
        public string Value { get; set; }
        public string Vocabulary { get; set; }

        public static IMultiLevelKeyField? FromString(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            int iSep = value.IndexOf(':');
            bool hasUri = value.StartsWith("http", StringComparison.OrdinalIgnoreCase) || value.StartsWith("doi:", StringComparison.OrdinalIgnoreCase) || value.Contains("://");

            if ((iSep == -1) || hasUri)
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