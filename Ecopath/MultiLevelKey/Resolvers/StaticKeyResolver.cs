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

    public IEnumerable<MultiLevelKeyMatch> FindAllMatches(MultiLevelKey input, KeyDomain domain)
    {
        List<MultiLevelKeyMatch> results = new();
        foreach (var key in m_mappings.Where(n => n.Domain == domain))
        {
            int score = MatchScore(input, key);
            if (score > 0)
                results.Add(new MultiLevelKeyMatch(key, score));
        }
        return results;
    }

    private int MatchScore(MultiLevelKey a, MultiLevelKey b)
    {
        int score = 0;

        foreach (KeyFieldDescriptor descr in m_fieldDescriptors)
        {
            MultiLevelKeyField? fva = a.GetField(descr.FieldName);
            MultiLevelKeyField? fvb = b.GetField(descr.FieldName);

            // Fail early
            if (descr.IsRequired && (fva == null || fvb == null))
                return 0;

            // No fuzzy matching
            string valueA = fva.ToString();
            if (!string.IsNullOrWhiteSpace(fva!.Value) && !string.IsNullOrWhiteSpace(fvb!.Value))
            {
                score += string.CompareOrdinal(fva!.Value, fvb!.Value) == 0 ? descr.Weight : 0;
            }
        }
        return score;
    }
}