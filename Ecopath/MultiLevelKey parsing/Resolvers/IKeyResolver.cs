/// <summary>
/// Interface for mapping a MultiLevelKey to EwE indices
/// </summary>
public interface IKeyResolver
{
    /// <summary>
    /// Reverse lookup
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    MultiLevelKey? GetKey(int index, KeyDomain domain);

    IEnumerable<(int index, int score, float propertion)> FindAllMatches(MultiLevelKey key, KeyDomain domain);
}