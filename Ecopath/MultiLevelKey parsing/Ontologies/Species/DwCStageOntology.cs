using Utilities;

public class DwCStageOntology : IStageOntology
{
    Dictionary<string, string[]> m_keys = new();

    public DwCStageOntology()
    {
        m_keys["juvenile"] = ["young", "juvenile", "small"];
        m_keys["adult"] = ["adult", "large", "old"];
        m_keys["larva"] = ["larva", "spawn", "hatchling"];
    }

    KeyDomain IOntology.KeyDomain => KeyDomain.Species;

    string IOntology.OntologyName => "dwc";

    public (string match, int score) MatchStage(string stage)
    {
        // Hack and slash version
        foreach (string key in m_keys.Keys)
        {
            (string Match, int Score) match = NameUtilities.FuzzyMatch(stage, m_keys[key]);
            if (match.Score > 80) return (key, match.Score);
        }
        return (string.Empty, 0);
    }
}
