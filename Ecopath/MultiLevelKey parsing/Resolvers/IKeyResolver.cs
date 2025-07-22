/// <summary>
/// Interface for mapping a MultiLevelKey to EwE indices
/// </summary>
public interface IKeyResolver
{
    IEnumerable<MultiLevelKeyMatch> FindAllMatches(MultiLevelKey key, KeyDomain domain);
}