using Ecopath.Models;

/// <summary>
/// A fuzzy implementation using partial field matching with weights and matchers
/// </summary>
public class FuzzyKeyResolver : IKeyResolver
{
    private readonly IEnumerable<MultiLevelKey> m_mappings;
    private readonly IEnumerable<KeyFieldDescriptor> m_fieldDescriptors;
    private readonly MatcherRegistry? m_matcherRegistry;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mappings"></param>
    /// <param name="descriptors"></param>
    /// <param name="matcherRegistry"></param>
    public FuzzyKeyResolver(IEnumerable<MultiLevelKey> mappings, IEnumerable<KeyFieldDescriptor> descriptors, MatcherRegistry? matcherRegistry = null)
    {
        m_mappings = mappings;
        m_fieldDescriptors = descriptors;
        m_matcherRegistry = matcherRegistry;
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

            var matcher = m_matcherRegistry?.Get(descr.FieldName) ?? new ExactFieldMatcher();
            double similarity = matcher.Score(descr.FieldName, valueA, valueB);

            score += (int)(descr.Weight * similarity);
        }
        return score;
    }

}