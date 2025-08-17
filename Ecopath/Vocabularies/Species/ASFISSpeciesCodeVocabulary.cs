using CsvHelper;
using System.Data;
using System.Globalization;
using Utilities;

public class ASFISSpeciesCodeVocabulary 
    : ControlledVocabularyBase, ISpeciesCodeVocabulary
{
    private const string COL_CODE = "Alpha3_Code";
    private const string COL_NAME = "Scientific_Name";

    public override IEnumerable<string> FieldNames => [COL_CODE, COL_NAME];
    public override string VocabularyName => "ASFIS";
    public override KeyDomain KeyDomain => KeyDomain.Country;
    public override KeyPurpose KeyPurpose => KeyPurpose.Country;

    protected override bool LoadFromSource()
    {
        if (m_data.Count > 0) return true;

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
                            m_data[NameUtilities.NormalizeName(scname)] = key;
                    }
                }
                catch (Exception ex)
                {
                    m_data.Clear();
                    return false;
                }
                return (m_data.Count > 0);
            }
        }
    }
    public (string match, int score) MatchSpeciesName(string speciesname, int iMinScore = 70) 
        => NameUtilities.FuzzyMatch(speciesname, this.m_data.Keys, iMinScore);

    public string CodeToSpecies(string speciescode)
    {
        foreach (string scnane in m_data.Keys)
        {
            MultiLevelKey key = m_data[scnane];
            if ((key != null) && (string.Compare(key.GetField(COL_CODE)!.ToString(false), speciescode, StringComparison.OrdinalIgnoreCase) == 0))
                return scnane;
        }
        return string.Empty;
    }

    public string SpeciesToCode(string speciesname)
    {
        string resolved = NameUtilities.FuzzyMatch(speciesname, this.m_data.Keys).BestMatch;

        if (string.IsNullOrWhiteSpace(resolved))
            return string.Empty;

        return m_data.TryGetValue(resolved, out MultiLevelKey? key) ? key.GetField(COL_CODE)!.ToString(false) : string.Empty;
    }

}
