using ControlledVocabularies.Core;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Match
{
    public interface IVocabularyMatcher
    {
        MatchResult Match(MultiLevelKey input, IControlledVocabulary sourceVocab, IControlledVocabulary targetVocab, int? minscore = null);
    }
}