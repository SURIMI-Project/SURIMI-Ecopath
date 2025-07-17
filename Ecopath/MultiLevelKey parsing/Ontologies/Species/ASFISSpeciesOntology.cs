using CsvHelper;
using System.Data;
using System.Globalization;
using Utilities;

public class ASFISSpeciesOntology 
    : ISpeciesOntology
{
    private Dictionary<string, string> m_keys = new();

    public KeyDomain KeyDomain => KeyDomain.Species;

    public string OntologyName => "ASFIS";

    public bool Load(string fin)
    {
        m_keys.Clear();

        using (var reader = new StreamReader(fin))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            // Do any configuration to `CsvReader` before creating CsvDataReader.
            using (var dr = new CsvDataReader(csv))
            {
                DataTable dt = new();
                dt.Columns.Add("Alpha3_Code", typeof(string));
                dt.Columns.Add("Scientific_Name", typeof(string));

                try
                {
                    dt.Load(dr);
                    foreach (DataRow dataRow in dt.Rows)
                    {
                        string code = (string)dataRow["Alpha3_Code"];
                        string scname = (string)dataRow["Scientific_Name"];

                        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(scname))
                            m_keys[NameUtilities.NormalizeName(scname)] = code;
                    }
                }
                catch (Exception ex)
                {
                    // NOP
                }
                return (m_keys.Count > 0);
            }
        }
    }
    public (string match, int score) MatchSpeciesName(string speciesname, int iMinScore = 70) 
        => NameUtilities.FuzzyMatch(speciesname, this.m_keys.Keys, iMinScore);

    public string CodeToSpecies(string speciescode)
    {
        foreach (string key in m_keys.Keys)
            if (string.Compare(m_keys[key], speciescode, StringComparison.OrdinalIgnoreCase) == 0)
                return key;
        return string.Empty;
    }

    public string SpeciesToCode(string speciesname)
    {
        string resolved = NameUtilities.FuzzyMatch(speciesname, this.m_keys.Keys).BestMatch;

        if (string.IsNullOrWhiteSpace(resolved))
            return string.Empty;

        return m_keys.TryGetValue(resolved, out var code) ? code : string.Empty;
    }

}
