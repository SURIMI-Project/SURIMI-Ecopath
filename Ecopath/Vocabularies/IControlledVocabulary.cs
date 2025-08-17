public interface IControlledVocabulary
{
    KeyDomain KeyDomain { get; }
    KeyPurpose KeyPurpose { get; }

    string VocabularyName { get; }

    bool Load();

    IEnumerable<MultiLevelKey> Records { get; }
    IEnumerable<string> FieldNames { get; }

    KeyFieldDescriptor? GetKeyFieldDescriptor(string FieldName);

    /// <summary>
    /// Dictionary(fieldName -> [vocabulary_name]:[field_name]) to facilitate direct look-ups
    /// </summary>
    Dictionary<string, string> ForeignKeyMap { get; }
}
