using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.ForeignKeys
{
    /// <summary>
    /// 
    /// </summary>
    public interface IForeignKeyResolver
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="record"></param>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        MatchResult TryResolve(MultiLevelKey record, IControlledVocabulary source, IControlledVocabulary target);
    }
}