public interface IControlledVocabulary
{
    KeyDomain KeyDomain { get; }
    string VocabularyName { get; }

    bool Load();
}
