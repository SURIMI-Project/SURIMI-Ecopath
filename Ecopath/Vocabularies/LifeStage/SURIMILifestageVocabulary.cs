using Utilities;

/// <summary>
/// A friendly and legible controlled dictionary to express life stages
/// </summary>
public class SURIMILifestageVocabulary 
    : ControlledVocabularyBase, ILifeStageVocabulary
{
    public SURIMILifestageVocabulary()
    {
    }

    public new KeyDomain KeyDomain => KeyDomain.Species;

    public new KeyPurpose KeyPurpose => KeyPurpose.LifeStage;

    public new string VocabularyName => "surimi.lifestage";

    public (string match, int score) MatchLifestage(string stage, int iMinScore = 70)
    {
        int bestScore = 0;
        string bestKey = "";

        foreach (string key in m_keys.Keys)
        {
            string compare = m_keys[key].GetField(SpeciesFields.Stage)!.ToString(false);
            var score = NameUtilities.TokenSetFuzzyMatch(stage, compare);
            if (score > bestScore)
            {
                bestKey = key;
                bestScore = score;
            }
        }
        if (bestScore >= iMinScore)
            return (bestKey, bestScore);
        return (string.Empty, 0);
    }

    public override bool Load()
    {
        m_keys.Clear();

        MultiLevelKey tmp = new() { Domain = KeyDomain.Species };

        m_keys["juvenile"] = MultiLevelKey.FromString (SpeciesFields.Stage + "=young juvenile small");
        m_keys["adult"] = MultiLevelKey.FromString(SpeciesFields.Stage + "=adult large old");
        m_keys["larva"] = MultiLevelKey.FromString(SpeciesFields.Stage + "=larva spawn hatchling");
        m_keys["egg"] = MultiLevelKey.FromString(SpeciesFields.Stage + "=egg");

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
