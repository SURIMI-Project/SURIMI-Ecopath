using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Match;
using ControlledVocabularies.Utils;
using System.Data;

namespace ControlledVocabularies.Vocabularies
{
    /// <summary>
    /// Foundation class for building specific <see cref="IControlledVocabulary"/>
    /// instances.
    /// </summary>
    /// <todo>
    /// Refactor for stream loading, caching, and using local fallback files.
    /// </todo>
    /// <todo>
    /// Enable multi-language support by duplicating textual columns (e.g., "name_ESP") and translating them.
    /// Translation can be handled by agent AIs (translate → verify → log → cache).
    /// Once added, translated columns become native vocabulary fields — no further adaptation required.
    /// </todo>
    public abstract class ControlledVocabularyBase : IControlledVocabulary
    {
        #region Private classes 

        protected class DataTableKeyFieldDescriptor : KeyFieldDescriptor
        {
            public DataTableKeyFieldDescriptor(DataColumn column, KeyDomain domain, KeyPurpose purpose, bool isRequired = false, int weight = 1, MatchStrategy strategy = MatchStrategy.Exact)
                : base(column.ColumnName, domain, purpose, isRequired, weight, strategy)
            {
                Column = column ?? throw new ArgumentNullException(nameof(column));
            }

            /// <summary>
            /// The DataColumn this descriptor is associated with.
            /// </summary>
            public DataColumn Column { get; }
        }

        private class KeyFieldIndexer
        {
            public bool BuildIndex(string fieldName, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor)
            {
                List<string> values = new();
                int totalRecords = records.Count();

                // Aggregate values per field
                foreach (var record in records)
                {
                    var field = record.GetField(fieldName);
                    if (field == null) continue; // skip, do NOT bail

                    string value = field.Value?.Trim() ?? String.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        values.Add(value);
                }

                int nonZero = values.Count;
                int distinct = values.Distinct().Count();
                double nonZeroRatio = totalRecords > 0 ? (double)nonZero / totalRecords : 0.0;
                double uniquenessRatio = nonZero > 0 ? (double)distinct / nonZero : 0.0;
                int avgLen = nonZero > 0 ? (int)values.Average(v => v.Length) : 0;

                descriptor.AvgLength = avgLen;
                descriptor.DistinctValueCount = distinct;
                descriptor.NonZeroRatio = nonZeroRatio;
                descriptor.UniquenessRatio = uniquenessRatio;

                //// Purpose / Domain aware pinning
                //// If this column’s *field* domain != vocab domain, treat as FK-like → Exact only
                //// (Field domain comes from the per-field descriptor already attached via the registry)
                //bool isForeignToVocab = descriptor.Domain != KeyDomain && descriptor.Domain != KeyDomain.NotSet;

                //if (isForeignToVocab)
                //{
                //    descriptor.Strategy = MatchStrategy.Exact;
                //    return true;
                //}

                //// For identifier-ish purposes, bias strongly to Exact (optionally add Fuzzy for short labels)
                //bool looksLikeIdPurpose =
                //    descriptor.Purpose == KeyPurpose.Species ||
                //    descriptor.Purpose == KeyPurpose.Gear ||
                //    descriptor.Purpose == KeyPurpose.Country ||
                //    descriptor.Purpose == KeyPurpose.Market;

                //if (looksLikeIdPurpose)
                //{
                //    // Let content heuristics still refine, but ensure Exact is present
                //    var inferred = InferStrategies(values, avgLen, distinct, uniquenessRatio, nonZeroRatio);
                //    descriptor.Strategy = inferred | MatchStrategy.Exact;
                //    return true;
                //}

                // Infer strategy is not already pinned
                if (descriptor.Strategy == MatchStrategy.None) 
                    descriptor.Strategy = InferStrategies(values, avgLen, distinct, uniquenessRatio, nonZeroRatio);

                var salience = ComputeSalience(avgLen, uniquenessRatio, nonZeroRatio);

                if (descriptor.UseAutoWeight)
                {
                    var baseW = BaseWeightFor(descriptor.Strategy);              // 1..10
                    var auto = (int)Math.Round(baseW * salience);                // still 1..10-ish
                    descriptor.AutoWeight = Math.Clamp(auto, 1, 10);             // keep it tight
                }
                return true;
            }

            /// <summary>
            /// Infer a field intercomparison strategy based on a set of simple rules
            /// related to field length, content, and content uniqueness.
            /// </summary>
            /// <param name="values"></param>
            /// <param name="avgLen"></param>
            /// <param name="distinct"></param>
            /// <param name="uniquenessRatio"></param>
            /// <param name="nonZeroRatio"></param>
            /// <returns></returns>
            private static MatchStrategy InferStrategies(List<string> values, int avgLen, int distinct, double uniquenessRatio, double nonZeroRatio)
            {
                // Very sparse fields: skip outright
                if (nonZeroRatio < 0.1)
                    return MatchStrategy.None;

                // Quick structural signals
                bool mostlyNumeric = values.All(v => v.All(char.IsDigit));
                double upperRatio = UppercaseRatio(values);           // 0..1
                double avgWordCount = AverageWordCount(values);       // ~0 for codes
                bool looksLikeUri = values.Any(LooksLikeUriOrDoi);    // any URI/DOI present?

                // URIs/DOIs: almost never worth cross-vocab matching
                if (looksLikeUri)
                    return MatchStrategy.None;

                MatchStrategy strategy = MatchStrategy.None;

                // Short, uppercase, highly-unique fields => codes (e.g., ASFIS alpha3)
                // thresholds: len<=5, upper>=0.9, unique>=0.7, words<=1.1
                if (avgLen <= 5 && upperRatio >= 0.9 && uniquenessRatio >= 0.7 && avgWordCount <= 1.1)
                    strategy |= MatchStrategy.Exact;

                // Medium-length labels: allow exact + fuzzy for robust name matching
                if (avgLen > 5 && avgLen <= 25)
                {
                    strategy |= MatchStrategy.Exact;
                    if (!mostlyNumeric)
                        strategy |= MatchStrategy.Fuzzy;
                }

                // Long text AND very high distinct count → keyword/token overlap
                if (avgLen > 25 && distinct > 100)
                    strategy |= MatchStrategy.Keyword | MatchStrategy.TokenOverlap;

                // Pure numeric fields: enable numeric-range semantics (optional downstream)
                if (mostlyNumeric)
                    strategy |= MatchStrategy.NumericRange;

                // If nothing triggered, fall back to Exact for safety on mid/short labels
                if (strategy == MatchStrategy.None && avgLen > 0 && avgLen <= 25)
                    strategy |= MatchStrategy.Exact;

                return strategy;
            }

            // --- helpers ---

            private static double UppercaseRatio(List<string> values)
            {
                if (values.Count == 0) return 0.0;
                int upperish = 0;
                foreach (var v in values)
                {
                    // consider A–Z and digits/underscores as "code-friendly"
                    bool ok = v.All(ch => char.IsUpper(ch) || char.IsDigit(ch) || ch == '_' || ch == '-');
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
                    // split on whitespace; treat empty as 0
                    var wc = string.IsNullOrWhiteSpace(v) ? 0 : v.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
                    sum += wc;
                }
                return sum / values.Count;
            }

            private static bool LooksLikeUriOrDoi(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                s = s.Trim();

                // very lightweight checks to avoid regex overhead unless needed
                if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    s.StartsWith("doi:", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("//"))
                    return true;

                // optional: compact regex for http(s) or doi (keep simple to avoid false positives)
                // return Regex.IsMatch(s, @"^(https?://|doi:)", RegexOptions.IgnoreCase);

                return false;
            }

            static int BaseWeightFor(MatchStrategy strategy)
            {
                // Choose primary flag by priority (first one that applies).
                if (strategy.HasFlag(MatchStrategy.Exact)) return 10;
                if (strategy.HasFlag(MatchStrategy.Synonym)) return 9;   // if used
                if (strategy.HasFlag(MatchStrategy.Fuzzy)) return 7;
                if (strategy.HasFlag(MatchStrategy.TokenOverlap)) return 5;
                if (strategy.HasFlag(MatchStrategy.Keyword)) return 4;
                if (strategy.HasFlag(MatchStrategy.NumericRange)) return 6;
                return 3; // conservative default
            }

            static double ComputeSalience(int avgLen, double uniqueness, double coverage)
            {
                // Favor short fields; penalize long prose. Pivot around ~12 chars.
                double lengthFactor = 12.0 / Math.Max(12.0, avgLen <= 0 ? 12.0 : avgLen); // ~0..1
                                                                                          // Keep it intuitive and smooth; weights sum to 1
                const double wLen = 0.6, wUniq = 0.3, wCov = 0.1;

                double raw = (wLen * lengthFactor) + (wUniq * uniqueness) + (wCov * coverage);
                return Math.Clamp(raw, 0.15, 1.0); // don’t zero out usable columns
            }

        }
        #endregion // Private classes

        #region State variables

           /// <summary>
        /// The data in the vocabulary, cached
        /// </summary>
        private Dictionary<string, MultiLevelKey> m_data = new();

        protected Dictionary<string, DataTableKeyFieldDescriptor> m_descriptors = new();

        #endregion // State variables

        #region Mandatory overrides

        /// <summary>
        /// 
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="domain"></param>
        /// <param name="purpose"></param>
        /// <param name="isRequired"></param>
        /// <param name="weight"></param>
        /// <param name="strategy">Strategy to match a field. 
        /// Defaults to <see cref="MatchStrategy.None"/> for <see cref="KeyFieldIndexer"/> appraisal.</param>
        /// <returns></returns>
        /// <todo>
        /// The MultiLevelKey system is designed to work with strings only for identifying fields. There is no
        /// need (yet) to work with datatable columns other than strings. This may change at some point.
        /// </todo>
        protected DataTableKeyFieldDescriptor AddField(string columnName, KeyDomain domain, KeyPurpose purpose, bool isRequired = false, int weight = 1, MatchStrategy strategy = MatchStrategy.None)
        {
            columnName = StringHelpers.NormalizeName(columnName);

            if (!Table.Columns.Contains(columnName))
            {
                Table.Columns.Add(columnName, typeof(string));
            }

            DataColumn col = Table.Columns[columnName]!;
            var descriptor = new DataTableKeyFieldDescriptor(col, domain, purpose, isRequired, weight, strategy);
            m_descriptors[columnName] = descriptor;
            return descriptor;
        }

        /// <inheritdoc cref="IControlledVocabulary.Domain"/>
        public abstract KeyDomain Domain { get; }

        /// <inheritdoc cref="IControlledVocabulary.Purpose"/>
        public abstract KeyPurpose Purpose { get; }

        /// <inheritdoc cref="IControlledVocabulary.VocabularyName"/>
        public abstract string VocabularyName { get; }

        /// <inheritdoc cref="IControlledVocabulary.FieldNames"/>
        public IEnumerable<string> FieldNames => m_descriptors.Keys;

        /// <inheritdoc cref="IControlledVocabulary.CodeFieldName"/>
        public abstract string CodeFieldName { get; }

        /// <summary>
        /// The data in the vocabulary, in table format.
        /// </summary>
        protected DataTable Table { get; set; } = new();

        /// <summary>
        /// Load the vocavulary from its source.
        /// </summary>
        /// <returns>True if successful.</returns>
        protected abstract bool LoadFromSource();

        #endregion // Mandatory overrides

        #region Accessors

        /// <inheritdoc cref="IControlledVocabulary.Records"/>
        public IEnumerable<MultiLevelKey> Records
        {
            get 
            { 
                if (m_data.Values.Count == 0)
                {
                    // Do a bit of magic here. First, build a custom, local, and one-off key field descriptor registry
                    KeyFieldDescriptorRegistry regTemp = new();
                    foreach (var descr in m_descriptors.Values)
                        regTemp.Register(descr);

                    // Use this to generate key descriptor-aware MultiLevelKey instances
                    foreach (DataRow drow in Table.Rows)
                    {
                        var raw = drow[CodeFieldName];
                        var id = raw?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(id))
                        {
                            // log and skip
                            continue;
                        }
                        m_data[id] = MultiLevelKey.FromDataRow(drow, Domain, regTemp);
                    }
                }
                return m_data.Values; 
            }
        }

        #endregion // Accessors

        #region Base functionality

        /// <inheritdoc cref="IControlledVocabulary.GetKeyFieldDescriptor"/>
        public KeyFieldDescriptor? GetKeyFieldDescriptor(string fieldName)
        {
            fieldName = StringHelpers.NormalizeName(fieldName);
            if (!m_descriptors.ContainsKey(fieldName))
                return null;
            return m_descriptors[fieldName];
        }

        /// <inheritdoc cref="IControlledVocabulary.Load"/>
        public bool Load()
        {
            m_data.Clear();
            m_descriptors.Clear();
            this.Table.Clear();
            this.Table.Columns.Clear();

            if (!LoadFromSource()) return false;

            KeyFieldIndexer indexer = new();
            foreach (string fieldName in FieldNames)
            {
                indexer.BuildIndex(fieldName, Records, m_descriptors[fieldName]);
            }

            return true;
        }

        /// <inheritdoc cref="IControlledVocabulary.FindCode"/>
        public string FindCode(string input)
        {
            int bestScore = 0;
            string bestCode = "";

            StrategyBasedMatcher matcher = new();

            foreach (string fieldName in FieldNames)
            {
                var descr = m_descriptors[fieldName];
                MatchResult? match = matcher.FindBestMatch(input, m_data.Values, descr);

                if (match != null)
                {
                    if (match.Score > bestScore)
                    {
                        bestScore = match.Score;
                        bestCode = match.MatchedKey!.GetField(CodeFieldName)!.Value;
                    }
                }
            }
            return bestCode;
        }

        #endregion // Base functionality

    }
}