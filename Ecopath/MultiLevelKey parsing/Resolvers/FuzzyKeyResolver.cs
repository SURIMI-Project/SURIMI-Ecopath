using Ecopath.Models;

/// <summary>
/// A fuzzy implementation using partial field matching with weights and matchers
/// </summary>
public class FuzzyKeyResolver : IKeyResolver
{
    private readonly IEnumerable<MultiLevelKey> m_mappings;
    private readonly Dictionary<string, int> m_fieldWeights;
    private readonly MatcherRegistry? m_matcherRegistry;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mappings"></param>
    /// <param name="customWeights">Custom weights per field. Field names must be lowercase.</param>
    /// <param name="matcherRegistry"></param>
    public FuzzyKeyResolver(IEnumerable<MultiLevelKey> mappings, Dictionary<string, int>? customWeights = null, MatcherRegistry? matcherRegistry = null)
    {
        m_mappings = mappings;
        m_fieldWeights = customWeights ?? DefaultWeights();
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
        foreach (string key in a.FieldNames())
        {
            string valueA = a.GetField(key);
            string valueB = b.GetField(key);
            var matcher = m_matcherRegistry?.Get(key) ?? new ExactFieldMatcher();
            double similarity = matcher.Score(key, valueA, valueB);
            // ToDo: safeguard that field weights are also specified as lowercase invariant
            score += (int)((m_fieldWeights.TryGetValue(key, out var weight) ? weight : 1) * similarity);
        }
        return score;
    }

    private static Dictionary<string, int> DefaultWeights() => new()
    {
        {SpeciesFields.SpeciesCode, 10},
        {SpeciesFields.Lifestage, 3},
        {SpeciesFields.Length, 3},
        {SpeciesFields.Age, 3},
        {FishingFields.GearCode, 10},
        {FishingFields.Flag, 10 },
        {"marketcode", 10}
    };
}