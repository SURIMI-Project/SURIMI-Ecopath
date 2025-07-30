using CsvHelper;
using System.Data;
using System.Globalization;
using Utilities;

public class ASFISSpeciesVocabulary 
    : ISpeciesCodeVocabulary
{
    private Dictionary<string, MultiLevelKey> m_keys = new();

    public KeyDomain KeyDomain => KeyDomain.Species;

    public string VocabularyName => "ASFIS";

    public bool Load()
    {
        if (m_keys.Count > 0) return true;

        string fin = @"Includes\ASFIS_sp_2024.csv";
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
                        var key = MultiLevelKey.FromDataRow(dataRow);
                        string code = (string)dataRow["Alpha3_Code"];
                        string scname = (string)dataRow["Scientific_Name"];

                        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(scname))
                            m_keys[NameUtilities.NormalizeName(scname)] = key;
                    }
                }
                catch (Exception ex)
                {
                    m_keys.Clear();
                    return false;

                }
                return (m_keys.Count > 0);
            }
        }
    }
    public (string match, int score) MatchSpeciesName(string speciesname, int iMinScore = 70) 
        => NameUtilities.FuzzyMatch(speciesname, this.m_keys.Keys, iMinScore);

    public string CodeToSpecies(string speciescode)
    {
        foreach (string scnane in m_keys.Keys)
        {
            MultiLevelKey key = m_keys[scnane];
            if ((key != null) && (string.Compare(key.GetField("Alpha3_Code")!.ToString(false), speciescode, StringComparison.OrdinalIgnoreCase) == 0))
                return scnane;
        }
        return string.Empty;
    }

    public string SpeciesToCode(string speciesname)
    {
        string resolved = NameUtilities.FuzzyMatch(speciesname, this.m_keys.Keys).BestMatch;

        if (string.IsNullOrWhiteSpace(resolved))
            return string.Empty;

        return m_keys.TryGetValue(resolved, out MultiLevelKey? key) ? key.GetField("Alpha3_Code")!.ToString(false) : string.Empty;
    }

}
