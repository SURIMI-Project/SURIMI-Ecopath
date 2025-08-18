/// <summary>
/// Interface for mapping a MultiLevelKey to other data sources
/// </summary>
public interface IKeyResolver
{
    IEnumerable<KeyResolverMatchResult> FindAllMatches(MultiLevelKey key, KeyDomain domain, int? minscore = null);
    KeyResolverMatchResult? FindBestMatch(MultiLevelKey input, KeyDomain domain, int? minscore = null);

}