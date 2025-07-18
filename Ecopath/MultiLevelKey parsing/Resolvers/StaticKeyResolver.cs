/// <summary>
/// A simple implementation based on exact field matching
/// </summary>
public class StaticKeyResolver : IKeyResolver
{
    private readonly IEnumerable<MultiLevelKey> m_mappings;

    public StaticKeyResolver(IEnumerable<MultiLevelKey> mappings)
    {
        m_mappings = mappings;
    }

    public IEnumerable<(MultiLevelKey key, int score)> FindAllMatches(MultiLevelKey key, KeyDomain domain)
    {
        var fieldsB = key.FieldNames().OrderBy(f => f).ToList();

        foreach (var kvp in m_mappings)
        {
            if (kvp.Domain != domain)
                continue;

            var fieldsA = kvp.FieldNames().OrderBy(f => f).ToList();

            if (fieldsA.Count != fieldsB.Count)
                continue;

            bool allEqual = true;
            for (int i = 0; i < fieldsA.Count; i++)
            {
                if (fieldsA[i] != fieldsB[i] || kvp.GetField(fieldsA[i]) != key.GetField(fieldsB[i]))
                {
                    allEqual = false;
                    break;
                }
            }

            if (allEqual)
                yield return (kvp, 1);
        }
    }
}