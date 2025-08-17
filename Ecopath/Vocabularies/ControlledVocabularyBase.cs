
public abstract class ControlledVocabularyBase: IControlledVocabulary
{
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

    public abstract KeyDomain KeyDomain { get; }

    public abstract KeyPurpose KeyPurpose { get; }

    public abstract string VocabularyName { get; }

    public IEnumerable<MultiLevelKey> Records => m_data.Values;

    public Dictionary<string, string> ForeignKeyMap => m_foreignKeyMap;

    public abstract IEnumerable<string> FieldNames { get; }

    public KeyFieldDescriptor? GetKeyFieldDescriptor(string FieldName)
    {
        if (!FieldNames.Contains(FieldName))
            return null;
        return m_fieldStats[FieldName];
    }

    public bool Load()
    {
        if (!LoadFromSource())
            return false;

        MultiLevelKeyIndexer indexer = new();
        foreach (string fieldName in FieldNames)
        {
            m_fieldStats[fieldName] = new KeyFieldDescriptor(fieldName);
            indexer.BuildIndex(fieldName, Records, m_fieldStats[fieldName]);
        }

        return true;
    }

    protected abstract bool LoadFromSource();
}
