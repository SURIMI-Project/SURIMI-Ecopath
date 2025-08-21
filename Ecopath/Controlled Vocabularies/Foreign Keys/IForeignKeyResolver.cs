using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.ForeignKeys
{
    public interface IForeignKeyResolver
    {
        MatchResult TryResolve(MultiLevelKey record,
                               IControlledVocabulary source,
                               IControlledVocabulary target);
    }
}