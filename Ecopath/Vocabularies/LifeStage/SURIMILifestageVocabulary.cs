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

    public override IEnumerable<string> FieldNames => [SpeciesFields.Stage];
    public override string VocabularyName => "surimi.lifestage";
    public override KeyDomain KeyDomain => KeyDomain.Species;
    public override KeyPurpose KeyPurpose => KeyPurpose.Lifestage;


    public (string match, int score) MatchLifestage(string stage, int iMinScore = 70)
    {
        int bestScore = 0;
        string bestKey = "";

        foreach (string key in m_data.Keys)
        {
            string compare = m_data[key].GetField(SpeciesFields.Stage)!.ToString(false);
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

    protected override bool LoadFromSource()
    {
        m_data.Clear();

        m_data["juvenile"] = MultiLevelKey.FromString (SpeciesFields.Stage + "=young juvenile small");
        m_data["adult"] = MultiLevelKey.FromString(SpeciesFields.Stage + "=adult large old");
        m_data["larva"] = MultiLevelKey.FromString(SpeciesFields.Stage + "=larva spawn hatchling");
        m_data["egg"] = MultiLevelKey.FromString(SpeciesFields.Stage + "=egg");

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
