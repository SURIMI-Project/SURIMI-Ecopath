/// <summary>
/// A simple implementation based on exact field matching
/// </summary>
public class StaticKeyResolver : IKeyResolver
{
    private readonly IEnumerable<MultiLevelKey> m_mappings;
    private readonly IEnumerable<KeyFieldDescriptor> m_fieldDescriptors;

    public StaticKeyResolver(IEnumerable<MultiLevelKey> mappings, IEnumerable<KeyFieldDescriptor> descriptors)
    {
        m_mappings = mappings;
        m_fieldDescriptors = descriptors;
    }

    public IEnumerable<(MultiLevelKey key, int score)> FindAllMatches(MultiLevelKey input, KeyDomain domain)
    {
        foreach (var key in m_mappings.Where(n => n.Domain == domain))
        {
            int score = MatchScore(input, key);
            if (score > 0)
                yield return (key, score);
        }
    }

    private int MatchScore(MultiLevelKey a, MultiLevelKey b)
    {
        int score = 0;

        foreach (KeyFieldDescriptor descr in m_fieldDescriptors)
        {
            string? valueA = a.GetField(descr.FieldName);
            string? valueB = b.GetField(descr.FieldName);

            // Fail early
            if (descr.IsRequired && (string.IsNullOrWhiteSpace(valueA) || string.IsNullOrEmpty(valueB)))
                return 0;

            // No fuzzy matching
            if (!string.IsNullOrWhiteSpace(valueA) && !string.IsNullOrWhiteSpace(valueB))
                score += string.CompareOrdinal(valueA, valueB) == 0 ? descr.Weight : 0;
        }
        return score;
    }
}