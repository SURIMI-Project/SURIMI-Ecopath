using ControlledVocabularies.Common;
using ControlledVocabularies.Core;

namespace ControlledVocabularies.Utils
{
    public class FieldFilter
    {
        public static List<string> ExtractFieldValues(IEnumerable<MultiLevelKey> records, string fieldName)
        {
            var values = new List<string>();
            var seen = new HashSet<string>();
            var maxSamples = LocalSettings.DefaultMaxSamples;
            var deduplicate = LocalSettings.DefaultDeduplicate;

            int remaining = (maxSamples <= 0) ? int.MaxValue : maxSamples;

            foreach (var rec in records)
            {
                if (remaining == 0) break;

                var f = rec.GetField(fieldName); // GetField() normalizes name internally
                var raw = f?.Value;
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var v = raw.Trim();
                if (v.Length == 0) continue;

                if (deduplicate)
                {
                    if (!seen.Add(v)) continue;
                }

                values.Add(v);
                if (remaining != int.MaxValue) remaining--;
            }

            return values;
        }

        /// <summary>
        /// Normalized extractor for comparison/matching scenarios.
        /// If maxSamples <= 0, uses all records.
        /// </summary>
        public static List<string> ExtractFieldValuesNormalized( IEnumerable<MultiLevelKey> records, string fieldName, FieldKind kind, bool caseSensitive, int maxSamples = 0, bool deduplicate = true)
        {
            var values = new List<string>();
            var seen = new HashSet<string>();
            int remaining = (maxSamples <= 0) ? int.MaxValue : maxSamples;

            foreach (var rec in records)
            {
                if (remaining == 0) break;

                var f = rec.GetField(fieldName);
                var raw = f?.Value;
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var normalized = FieldPolicy.ForValue(raw.Trim(), kind, caseSensitive);
                if (normalized.Length == 0) continue;

                if (deduplicate)
                {
                    if (!seen.Add(normalized)) continue;
                }

                values.Add(normalized);
                if (remaining != int.MaxValue) remaining--;
            }

            return values;
        }
    }
}
