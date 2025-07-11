/// <summary>
/// A fuzzy implementation using partial field matching with weights and matchers
/// </summary>
public class FuzzyKeyResolver : IKeyResolver
{
    private readonly List<MultiLevelKey> m_mappings;
    private readonly Dictionary<string, int> m_fieldWeights;
    private readonly MatcherRegistry? m_matcherRegistry;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mappings"></param>
    /// <param name="customWeights">Custom weights per field. Field names must be lowercase.</param>
    /// <param name="matcherRegistry"></param>
    public FuzzyKeyResolver(
        List<MultiLevelKey> mappings,
        Dictionary<string, int>? customWeights = null,
        MatcherRegistry? matcherRegistry = null)
    {
        m_mappings = mappings;
        m_fieldWeights = customWeights ?? DefaultWeights();
        m_matcherRegistry = matcherRegistry;
    }

    public IEnumerable<(int index, int score, float propertion)> FindAllMatches(MultiLevelKey input, KeyDomain domain)
    {
        foreach (var key in m_mappings.Where(n => n.Domain == domain))
        {
            int score = MatchScore(input, key);
            if (score > 0)
                yield return (key.Index, score, key.Propertion);
        }
    }

    private int MatchScore(MultiLevelKey a, MultiLevelKey b)
    {
        int score = 0;
        foreach (var kvpair in a.Fields)
        {
            if (b.Fields.TryGetValue(kvpair.Key, out var valB))
            {
                var matcher = m_matcherRegistry?.Get(kvpair.Key) ?? new ExactFieldMatcher();
                double similarity = matcher.Score(kvpair.Key, kvpair.Value, valB);

                score += (int)((m_fieldWeights.TryGetValue(kvpair.Key.ToLower(), out var weight) ? weight : 1) * similarity);
            }
        }
        return score;
    }

    private static Dictionary<string, int> DefaultWeights() => new()
    {
        {"speciescode", 10},
        {"stage", 3},
        {"length", 3},
        {"age", 3},
        {"gearcode", 10},
        {"flag", 10 },
        {"marketcode", 10}
    };

    public MultiLevelKey? GetKey(int index, KeyDomain domain)
    {
        return m_mappings.FirstOrDefault(pair => (pair.Index == index && pair.Domain == domain));
    }
}