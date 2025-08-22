using System.Globalization;
using ControlledVocabularies.Core;

namespace ControlledVocabularies.Descriptors
{
    internal sealed class KeyFieldIndexer : IKeyFieldIndexer
    {
        /// <summary>
        /// Cut-off below which no strategy will be assigned
        /// </summary>
        private const double SparseCutoff = 0.10;
        /// <summary>
        /// fraction of distinct/nonzero for code-ish
        /// </summary>
        private const double CodeUniqMin = 0.70;
        /// <summary>
        /// fraction of “upperish” values for code-ish
        /// </summary>
        private const double UpperMin = 0.90; 
        /// <summary>
        /// Max lengths for code fields
        /// </summary>
        private const int CodeLenMax = 6;

        /// <summary>
        /// Index a field from a collection of <see cref="MultiLevelKey">keys</see> into a 
        /// provided descriptor.
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="records"></param>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public bool BuildIndex(string fieldName, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor)
        {
            var values = new List<string>();
            int total = 0;
            foreach (var r in records)
            {
                total++;
                var s = r.GetField(fieldName)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(s)) values.Add(s!);
            }

            int nonZero = values.Count;
            int distinct = values.Distinct(StringComparer.Ordinal).Count();
            double nonZeroRatio = total > 0 ? (double)nonZero / total : 0.0;
            double uniquenessRatio = nonZero > 0 ? (double)distinct / nonZero : 0.0;
            int avgLen = nonZero > 0 ? (int)values.Average(v => v.Length) : 0;

            double upperRatio = UppercaseRatio(values);
            double avgWordCount = AverageWordCount(values);
            bool anyUri = values.Any(LooksLikeUriOrDoi);
            bool allNumeric = values.Count > 0 && values.All(IsNumeric);

            // publish stats
            descriptor.AvgLength = avgLen;
            descriptor.DistinctValueCount = distinct;
            descriptor.NonZeroRatio = nonZeroRatio;
            descriptor.UniquenessRatio = uniquenessRatio;

            // infer kind (only if unknown)
            if (descriptor.Kind == FieldKind.Unknown)
                descriptor.Kind = InferKind(avgLen, uniquenessRatio, upperRatio, anyUri, allNumeric);

            // infer strategy (only if none set)
            if (descriptor.Strategy == MatchStrategy.None)
                descriptor.Strategy = InferStrategies(descriptor.Kind, avgLen, distinct, nonZeroRatio, uniquenessRatio);

            // auto-weight
            if (descriptor.UseAutoWeight)
            {
                var baseW = BaseWeightFor(descriptor.Strategy); // 1..10
                var sal = ComputeSalience(avgLen, uniquenessRatio, nonZeroRatio);
                descriptor.AutoWeight = Math.Clamp((int)Math.Round(baseW * sal), 1, 10);
            }

            return true;
        }

        // --- inference helpers ---

        private static FieldKind InferKind(int avgLen, double uniq, double upper, bool anyUri, bool allNumeric)
        {
            if (anyUri) return FieldKind.Uri;
            if (allNumeric) return FieldKind.Numeric;
            bool codeish = avgLen <= CodeLenMax && upper >= UpperMin && uniq >= CodeUniqMin;
            return codeish ? FieldKind.Code : FieldKind.Label;
        }

        private static MatchStrategy InferStrategies(FieldKind kind, int avgLen, int distinct, double nonZeroRatio, double uniq)
        {
            if (nonZeroRatio < SparseCutoff || kind == FieldKind.Uri)
                return MatchStrategy.None;

            MatchStrategy s = MatchStrategy.None;

            if (kind == FieldKind.Code && avgLen <= CodeLenMax && uniq >= CodeUniqMin)
                s |= MatchStrategy.Exact;

            if (avgLen > 5 && avgLen <= 25)
            {
                s |= MatchStrategy.Exact;
                if (kind != FieldKind.Numeric) s |= MatchStrategy.Fuzzy;
            }

            if (avgLen > 25 && distinct > 100)
                s |= MatchStrategy.Keyword | MatchStrategy.TokenOverlap;

            if (kind == FieldKind.Numeric)
                s |= MatchStrategy.NumericRange;

            if (s == MatchStrategy.None && avgLen > 0 && avgLen <= 25)
                s |= MatchStrategy.Exact;

            return s;
        }

        // --- scoring helpers (kept private here) ---

        private static int BaseWeightFor(MatchStrategy strategy)
        {
            if (strategy.HasFlag(MatchStrategy.Exact)) return 10;
            if (strategy.HasFlag(MatchStrategy.Synonym)) return 9;
            if (strategy.HasFlag(MatchStrategy.Fuzzy)) return 7;
            if (strategy.HasFlag(MatchStrategy.TokenOverlap)) return 5;
            if (strategy.HasFlag(MatchStrategy.Keyword)) return 4;
            if (strategy.HasFlag(MatchStrategy.NumericRange)) return 6;
            return 3;
        }

        private static double ComputeSalience(int avgLen, double uniqueness, double coverage)
        {
            double lengthFactor = 12.0 / Math.Max(12.0, avgLen <= 0 ? 12.0 : avgLen);
            const double wLen = 0.6, wUniq = 0.3, wCov = 0.1;
            double raw = (wLen * lengthFactor) + (wUniq * uniqueness) + (wCov * coverage);
            return Math.Clamp(raw, 0.15, 1.0);
        }

        // --- low-level utilities (unchanged behavior) ---

        private static double UppercaseRatio(List<string> values)
        {
            if (values.Count == 0) return 0.0;
            int upperish = 0;
            foreach (var v in values)
            {
                bool ok = v.All(ch => char.IsUpper(ch) || char.IsDigit(ch) || ch is '_' or '-');
                if (ok) upperish++;
            }
            return (double)upperish / values.Count;
        }

        private static double AverageWordCount(List<string> values)
        {
            if (values.Count == 0) return 0.0;
            double sum = 0;
            foreach (var v in values)
            {
                var wc = string.IsNullOrWhiteSpace(v) ? 0
                    : v.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
                sum += wc;
            }
            return sum / values.Count;
        }

        private static bool LooksLikeUriOrDoi(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("doi:", StringComparison.OrdinalIgnoreCase)
                || s.Contains("//");
        }

        private static bool IsNumeric(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim().Replace(',', '.');
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }
    }
}
