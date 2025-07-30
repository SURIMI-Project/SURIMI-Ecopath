using Newtonsoft.Json;
using Utilities;

public class NERCLifeStageVocabulary : ILifeStageVocabulary
{
    // Code -> selected fields (ID, label)
    private Dictionary<string, MultiLevelKey> m_keys = new();

    public KeyDomain KeyDomain => KeyDomain.Species;
    public string VocabularyName => "NERC.S11";

    public bool Load()
    {
        // Live link, keep around for when reloading will appear:
        // https://vocab.nerc.ac.uk/collection/S11/current/?_profile=nvs&_mediatype=application/ld+json

        string path = @"Includes\S11_lifestages.jsonld";

        try
        {
            string json = File.ReadAllText(path);
            dynamic skos = JsonConvert.DeserializeObject(json)!;

            foreach (var concept in skos["@graph"])
            {
                string id = concept["@id"] ?? "";
                string label = concept["prefLabel"]?["@value"] ?? "";

                if (id.Contains("/S11") && !string.IsNullOrEmpty(label))
                {
                    var key = new MultiLevelKey();
                    key.SetField("ID", id);
                    key.SetField("Label", label);

                    m_keys[label.ToLowerInvariant()] = key;
                }
            }
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