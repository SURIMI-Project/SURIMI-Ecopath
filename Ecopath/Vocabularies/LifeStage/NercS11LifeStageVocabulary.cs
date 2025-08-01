using Newtonsoft.Json;
using Utilities;

public class NERCLifeStageVocabulary 
    : ControlledVocabularyBase, ILifeStageVocabulary
{
    public new KeyDomain KeyDomain => KeyDomain.Species;
    public new KeyPurpose KeyPurpose => KeyPurpose.LifeStage;

    public new string VocabularyName => "NERC.S11";

    public override bool Load()
    {
        // Live link, keep around for when reloading will appear:
        // https://vocab.nerc.ac.uk/collection/S11/current/?_profile=nvs&_mediatype=application/ld+json

        string path = @"Includes\NercS11_lifestages.jsonld";

        try
        {
            string json = File.ReadAllText(path);
            dynamic skos = JsonConvert.DeserializeObject(json)!;

            foreach (var concept in skos["@graph"])
            {
                string id = concept["@id"] ?? "";
                string label = string.Empty;

                var bucket = concept["skos:prefLabel"];
                try
                {
                    if (bucket != null)
                       label = bucket["@value"] ?? "";
                }
                catch (Exception ex)
                {
                    // Swallow this
                }

                if (id.Contains("/S11") && !string.IsNullOrEmpty(label))
                {
                    var key = new MultiLevelKey();
                    key.SetField("ID", id);
                    key.SetField("Label", label);

                    m_keys[label.ToLowerInvariant()] = key;
                }
            }
            m_fieldIndex = VocabularyFieldIndex.FromData(m_keys.Values);
            return m_keys.Count > 0;
        }
        catch (Exception ex)
        {
            m_keys.Clear();
            return false;
        }
    }

    //public string LabelToURI(string label)
    //    => m_keys.TryGetValue(label.ToLowerInvariant(), out var key)
    //        ? key.GetField("ID")!.ToString(false)
    //        : string.Empty;

    //public string URIToLabel(string uri)
    //    => m_keys.Values.FirstOrDefault(k => k.GetField("ID")!.ToString(false) == uri)
    //       ?.GetField("Label")!.ToString(false) ?? string.Empty;

    public (string match, int score) MatchLifestage(string stage, int iMinScore = 70)
    {
        int bestScore = 0;
        string bestKey = "";

        foreach (string key in m_keys.Keys)
        {
            string compare = m_keys[key].GetField("Label")!.ToString(false);
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

    public string CodeToLifeStage(string lifeStageCode)
    {
        return m_keys.TryGetValue(NameUtilities.NormalizeName(lifeStageCode), out var key) ? key.GetField("Label")!.ToString(false) : string.Empty;
    }

    public string LifeStageToCode(string LifeStage)
    {
        (string match, int score) = MatchLifestage(LifeStage);
        return (score > 0) ? match : string.Empty;
    }
}