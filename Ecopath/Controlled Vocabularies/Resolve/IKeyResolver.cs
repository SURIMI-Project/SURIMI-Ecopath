using ControlledVocabularies.Core;

namespace ControlledVocabularies.Resolve
{
    /// <summary>
    /// Interface for mapping a MultiLevelKey to other data sources
    /// </summary>
    public interface IKeyResolver
    {
        IEnumerable<KeyResolverMatchResult> FindAllMatches(MultiLevelKey key, int? minscore = null);
        KeyResolverMatchResult? FindBestMatch(MultiLevelKey input,  int? minscore = null);

    }
}