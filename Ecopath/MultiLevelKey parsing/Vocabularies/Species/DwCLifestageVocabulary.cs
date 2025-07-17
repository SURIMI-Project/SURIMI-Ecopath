using Utilities;

public class DwCLifestageVocabulary : ILifestageVocabulary
{
    Dictionary<string, string[]> m_keys = new();

    public DwCLifestageVocabulary()
    {
        m_keys["juvenile"] = ["young", "juvenile", "small"];
        m_keys["adult"] = ["adult", "large", "old"];
        m_keys["larva"] = ["larva", "spawn", "hatchling"];
        m_keys["egg"] = ["egg"];
    }

    public KeyDomain KeyDomain => KeyDomain.Species;

    public string VocabularyName => "dwc.lifestage";

    public (string match, int score) MatchLifestage(string stage, int iMinScore = 70)
    {
        if (!string.IsNullOrWhiteSpace(stage))
        {
            stage = NameUtilities.NormalizeName(stage);

            string bestKey = string.Empty;
            int bestScore = 0;

            // Hack and slash version
            foreach (string bit in stage.Split(" ", StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (string key in m_keys.Keys)
                {
                    (string Match, int Score) match = NameUtilities.FuzzyMatch(bit, m_keys[key]);
                    if (match.Score > bestScore)
                    {
                        bestKey = key;
                        bestScore = match.Score;
                    }
                }
            }

            if (bestScore >= iMinScore)
                return (bestKey, bestScore);
        }
        return (string.Empty, 0);
    }
}
