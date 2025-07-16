using CsvHelper;
using System.Data;
using System.Globalization;
using Utilities;

public class ASFISSpeciesOntology 
    : ISpeciesOntology
{
    private Dictionary<string, string> m_keys = new();

    KeyDomain IOntology.KeyDomain => KeyDomain.Species;

    string IOntology.OntologyName => "ASFIS";

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

    public (string match, int score) MatchSpeciesName(string speciesname) => NameUtilities.FuzzyMatch(speciesname, this.m_keys.Keys);

    public (string match, int score) MatchSpeciesCode(string speciescode)
    {
        foreach (string key in this.m_keys.Keys)
        {
            if (string.Compare(speciescode, this.m_keys[key], StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return (key, 1);
            } 
        }
        return (string.Empty, 0);
    }
}
