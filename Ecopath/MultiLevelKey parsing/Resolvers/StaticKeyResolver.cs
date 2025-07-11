/// <summary>
/// A simple implementation based on exact field matching
/// </summary>
public class StaticKeyResolver : IKeyResolver
{
    private readonly List<MultiLevelKey> m_mappings;

    public StaticKeyResolver(List<MultiLevelKey> mappings)
    {
        m_mappings = mappings;
    }

    public MultiLevelKey? GetKey(int index, KeyDomain domain)
    {
        return m_mappings.FirstOrDefault(pair => (pair.Index == index && pair.Domain == domain));
    }

    public IEnumerable<(int index, int score, float propertion)> FindAllMatches(MultiLevelKey key, KeyDomain domain)
    {
        foreach (var kvp in m_mappings.Where(n => n.Domain == domain))
            if (kvp.Fields.OrderBy(k => k.Key).SequenceEqual(key.Fields.OrderBy(k => k.Key)))
                yield return (kvp.Index, 1, kvp.Propertion);
    }
}