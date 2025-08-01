
public abstract class ControlledVocabularyBase: IControlledVocabulary
{
    protected Dictionary<string, MultiLevelKey> m_keys = new();
    protected Dictionary<string, string> m_foreignKeyMap = new();
    protected VocabularyFieldIndex? m_fieldIndex = null;

    public KeyDomain KeyDomain => throw new NotImplementedException();

    public KeyPurpose KeyPurpose => throw new NotImplementedException();

    public string VocabularyName => throw new NotImplementedException();

    public IEnumerable<MultiLevelKey> Records => m_keys.Values;

    public VocabularyFieldIndex? FieldIndex => m_fieldIndex;

    public Dictionary<string, string> ForeignKeyMap => m_foreignKeyMap;

    public abstract bool Load();
}
