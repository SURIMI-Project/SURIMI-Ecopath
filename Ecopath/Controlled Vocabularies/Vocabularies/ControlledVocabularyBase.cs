using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.ForeignKeys;
using ControlledVocabularies.Match;
using ControlledVocabularies.Utils;
using Ecopath.Services;
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
            columnName = FieldPolicy.ForSchema(columnName);

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
            fieldName = FieldPolicy.ForSchema(fieldName);
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

            var indexer = GlobalServiceLocator.Get<IKeyFieldIndexer>() ?? new KeyFieldIndexer();
            foreach (string fieldName in FieldNames)
            {
                indexer.BuildIndex(fieldName, Records, m_descriptors[fieldName]);
            }

#if DEBUG
            System.Diagnostics.Debug.Assert(FieldNames.All(fn => fn == FieldPolicy.ForSchema(fn)));
#endif

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

        /// <inheritdoc/>
        public bool SetFK(string sourceFieldName, IControlledVocabulary target, string targetFieldName)
        {
            if (target == null) return false;

            // normalize to SCHEMA space
            var srcFieldSchema = FieldPolicy.ForSchema(sourceFieldName);
            var tgtFieldSchema = FieldPolicy.ForSchema(targetFieldName);
            var tgtVocabSchema = FieldPolicy.ForSchema(target.VocabularyName);

            // validate existence
            if (!m_descriptors.ContainsKey(srcFieldSchema)) return false;
            if (!target.FieldNames.Contains(tgtFieldSchema)) return false;

            var descr = m_descriptors[srcFieldSchema];
            var prev = descr.ForeignKey;

            var spec = new ForeignKeySpec
            {
                TargetVocabulary = target.VocabularyName,
                TargetField = tgtFieldSchema,
                TargetDomain = target.Domain,
                TargetPurpose = target.Purpose,
                Strict = true
            };

            if (prev is not null && prev.Equals(spec)) return true;

            return TryAttachForeignKey(srcFieldSchema, spec);
        }

        /// <inheritdoc/>
        public bool RemoveFK(string sourceFieldName, string? targetVocabularyName = null)
        {
            var srcFieldSchema = FieldPolicy.ForSchema(sourceFieldName);
            if (!m_descriptors.TryGetValue(srcFieldSchema, out var descr)) return false;

            var current = descr.ForeignKey;
            if (current == null) return false;

            if (!string.IsNullOrWhiteSpace(targetVocabularyName))
            {
                var tv = FieldPolicy.ForSchema(targetVocabularyName);
                if (!string.Equals(current.TargetVocabulary, tv, StringComparison.Ordinal))
                    return false; // FK exists but points elsewhere
            }

            descr.ForeignKey = null; // *chop*
            return true;
        }

        #endregion // Base functionality

        #region Internal helpers

        internal bool TryAttachForeignKey(string schemaFieldName, ForeignKeySpec spec)
        {
            if (string.IsNullOrWhiteSpace(schemaFieldName) || spec == null) return false;
            if (!m_descriptors.TryGetValue(schemaFieldName, out var descr)) return false;
            descr.ForeignKey = spec; // ForeignKeySpec stores schema-safe names
            return true;
        }

        #endregion // Internal helpers
    }
}