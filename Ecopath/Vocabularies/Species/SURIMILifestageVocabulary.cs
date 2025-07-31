using Utilities;

/// <summary>
/// A friendly and legible controlled dictionary to express life stages
/// </summary>
public class SURIMILifestageVocabulary : ILifeStageVocabulary
{
    Dictionary<string, string[]> m_keys = new();

    public SURIMILifestageVocabulary()
    {
    }

    public KeyDomain KeyDomain => KeyDomain.Species;

    public KeyPurpose KeyPurpose => KeyPurpose.LifeStage;

    public string VocabularyName => "surimi.lifestage";


    public IEnumerable<MultiLevelKey> Records
    { 
        get
        {
            List<MultiLevelKey> recs = new();
            foreach (string key in m_keys.Keys)
            {
                MultiLevelKey tmp = new() { Domain = KeyDomain.Species };
                tmp.SetField(SpeciesFields.Stage, key);
                recs.Add(tmp); 
            }
            return recs;
        }
    }


    public (string match, int score) MatchLifestage(string stage, int iMinScore = 70)
    {
        if (!string.IsNullOrWhiteSpace(stage))
        {
            stage = NameUtilities.NormalizeName(stage);

            string bestKey = string.Empty;
            int bestScore = 0;

            // Hack and slash version
            foreach (string bit in stage.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(bit))
                    continue;

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

    public bool Load()
    {
        m_keys.Clear();

        m_keys["juvenile"] = ["young", "juvenile", "small"];
        m_keys["adult"] = ["adult", "large", "old"];
        m_keys["larva"] = ["larva", "spawn", "hatchling"];
        m_keys["egg"] = ["egg"];

        return true;
    }

    public string CodeToLifeStage(string lifeStageCode)
    {
        return lifeStageCode;
    }

    public string LifeStageToCode(string LifeStage)
    {
        return LifeStage;
    }
}
