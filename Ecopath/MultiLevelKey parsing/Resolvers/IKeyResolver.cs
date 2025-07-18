/// <summary>
/// Interface for mapping a MultiLevelKey to EwE indices
/// </summary>
public interface IKeyResolver
{
    IEnumerable<(MultiLevelKey key, int score)> FindAllMatches(MultiLevelKey key, KeyDomain domain);
}