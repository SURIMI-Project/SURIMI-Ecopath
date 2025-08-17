using Ecopath.Models;

/// <summary>
/// A fuzzy implementation using partial field matching with weights and matchers
/// </summary>
public class FuzzyKeyResolver : IKeyResolver
{
    private readonly IEnumerable<MultiLevelKey> m_mappings;
    private readonly IEnumerable<KeyFieldDescriptor> m_fieldDescriptors;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mappings"></param>
    /// <param name="descriptors"></param>
    /// <param name="matcherRegistry"></param>
    public FuzzyKeyResolver(IEnumerable<MultiLevelKey> mappings, IEnumerable<KeyFieldDescriptor> descriptors)
    {
        m_mappings = mappings;
        m_fieldDescriptors = descriptors;
    }

    public IEnumerable<MultiLevelKeyMatch> FindAllMatches(MultiLevelKey input, KeyDomain domain)
    {
        foreach (var key in m_mappings.Where(n => n.Domain == domain))
        {
            int score = MatchScore(input, key);
            if (score > 0)
                yield return new MultiLevelKeyMatch(key, score);
        }
    }

    private int MatchScore(MultiLevelKey a, MultiLevelKey b)
    {
        int score = 0;
        FuzzyFieldMatcher matcher = new();

        foreach (KeyFieldDescriptor descr in m_fieldDescriptors) 
        {
            MultiLevelKeyField? valueA = a.GetField(descr.FieldName);
            MultiLevelKeyField? valueB = b.GetField(descr.FieldName);

            // Fail early
            if (descr.IsRequired && (valueA == null || valueB == null))
                return 0;

            double similarity = matcher.Score(valueA!.ToString(true), valueB!.ToString(true)) * 100;

            score += (int)(descr.Weight * similarity);
        }
        return score;
    }

}