public interface IVocabularyMatcher
{
    bool CanMatch(IControlledVocabulary sourceVocab, IControlledVocabulary targetVocab);

    VocabularyMatchResult Match(
        MultiLevelKey input,
        IControlledVocabulary sourceVocab,
        IControlledVocabulary targetVocab,
        int minScore = 80);
}