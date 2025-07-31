public interface IControlledVocabulary
{
    KeyDomain KeyDomain { get; }
    KeyPurpose KeyPurpose { get; }

    string VocabularyName { get; }

    bool Load();

    IEnumerable<MultiLevelKey> Records { get; }
}
