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
public abstract class ControlledVocabularyBase: IControlledVocabulary
{
    #region State variables

    /// <summary>
    /// The data in the vocabulary
    /// </summary>
    protected Dictionary<string, MultiLevelKey> m_data = new();

    /// <summary>
    /// Any foreign key mappings to other vocabularies
    /// </summary>
    protected Dictionary<string, string> m_foreignKeyMap = new();

    /// <summary>
    /// Data statistics, per column name
    /// </summary>
    protected Dictionary<string, KeyFieldDescriptor> m_fieldStats = new();

    #endregion // State variables

    #region Mandatory overrides

    /// <inheritdoc cref="IControlledVocabulary.KeyDomain"/>
    public abstract KeyDomain KeyDomain { get; }

    /// <inheritdoc cref="IControlledVocabulary.KeyPurpose"/>
    public abstract KeyPurpose KeyPurpose { get; }

    /// <inheritdoc cref="IControlledVocabulary.VocabularyName"/>
    public abstract string VocabularyName { get; }

    /// <inheritdoc cref="IControlledVocabulary.FieldNames"/>
    public abstract IEnumerable<string> FieldNames { get; }

    /// <inheritdoc cref="IControlledVocabulary.CodeFieldName"/>
    public abstract string CodeFieldName { get; }

    /// <summary>
    /// Load the vocavulary from its source.
    /// </summary>
    /// <returns>True if successful.</returns>
    protected abstract bool LoadFromSource();

    #endregion // Mandatory overrides

    #region Accessors

    /// <inheritdoc cref="IControlledVocabulary.Records"/>
    public IEnumerable<MultiLevelKey> Records => m_data.Values;

    /// <inheritdoc cref="IControlledVocabulary.ForeignKeyMap"/>
    public Dictionary<string, string> ForeignKeyMap => m_foreignKeyMap;

    #endregion // Accessors

    #region Base functionality

    /// <inheritdoc cref="IControlledVocabulary.GetKeyFieldDescriptor"/>
    public KeyFieldDescriptor? GetKeyFieldDescriptor(string FieldName)
    {
        if (!FieldNames.Contains(FieldName))
            return null;
        return m_fieldStats[FieldName];
    }

    /// <inheritdoc cref="IControlledVocabulary.Load"/>
    public bool Load()
    {
        if (!LoadFromSource())
            return false;

        MultiLevelKeyIndexer indexer = new();
        foreach (string fieldName in FieldNames)
        {
            m_fieldStats[fieldName] = new KeyFieldDescriptor(fieldName, KeyDomain, KeyPurpose);
            indexer.BuildIndex(fieldName, Records, m_fieldStats[fieldName]);
        }

        // Just to be sure
        foreach (MultiLevelKey key in Records)
            key.Domain = this.KeyDomain;

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
            var stats = m_fieldStats[fieldName];
            MatchResult? match = matcher.FindBestMatch(input, m_data.Values, stats);

            if (match != null)
            {
                if (match.Score > bestScore)
                {
                    bestScore = match.Score;
                    bestCode = match.MatchedKey!.GetField(this.CodeFieldName)!.Value;
                }
            }
        }
        return bestCode;
    }
    
    #endregion // Base functionality
}
