public interface IVocabularyMatcher
{
    MatchResult Match(MultiLevelKey input, IControlledVocabulary sourceVocab, IControlledVocabulary targetVocab, int? minscore = null);
}