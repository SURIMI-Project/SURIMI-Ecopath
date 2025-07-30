using CsvHelper;
using System.Data;
using System.Globalization;
using Utilities;

public class ASFISSpeciesVocabulary 
    : ISpeciesCodeVocabulary
{
    /// <summary>
    /// Scientific name -> complete record in the form of a MultiLevelKey
    /// </summary>
    private Dictionary<string, MultiLevelKey> m_keys = new();
    private const string COL_CODE = "Alpha3_Code";
    private const string COL_NAME = "Scientific_Name";

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
                dt.Columns.Add(COL_CODE, typeof(string));
                dt.Columns.Add(COL_NAME, typeof(string));

                try
                {
                    dt.Load(dr);
                    foreach (DataRow dataRow in dt.Rows)
                    {
                        var key = MultiLevelKey.FromDataRow(dataRow);
                        string code = (string)dataRow[COL_CODE];
                        string scname = (string)dataRow[COL_NAME];

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
            if ((key != null) && (string.Compare(key.GetField(COL_CODE)!.ToString(false), speciescode, StringComparison.OrdinalIgnoreCase) == 0))
                return scnane;
        }
        return string.Empty;
    }

    public string SpeciesToCode(string speciesname)
    {
        string resolved = NameUtilities.FuzzyMatch(speciesname, this.m_keys.Keys).BestMatch;

        if (string.IsNullOrWhiteSpace(resolved))
            return string.Empty;

        return m_keys.TryGetValue(resolved, out MultiLevelKey? key) ? key.GetField(COL_CODE)!.ToString(false) : string.Empty;
    }

}
