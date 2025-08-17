using CsvHelper;
using System.Data;
using System.Globalization;
using Utilities;

public class ISO3166CountryCodeVocabulary 
    : ControlledVocabularyBase, ICountryCodeVocabulary
{
    private const string COL_CODE = "alpha-3";
    private const string COL_NAME = "name";

    public override IEnumerable<string> FieldNames => [COL_CODE, COL_NAME];
    public override string VocabularyName => "ISO-3166";
    public override KeyDomain KeyDomain => KeyDomain.Country;
    public override KeyPurpose KeyPurpose => KeyPurpose.Country;


    protected override bool LoadFromSource()
    {
        if (m_data.Count > 0)
            return true;

        string fin = @"Includes\ISO-3166-Countries-with-Regional-Codes.csv";
        using (var reader = new StreamReader(fin))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
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
                        string code = dataRow[COL_CODE].ToString() ?? string.Empty;
                        string scname = dataRow[COL_NAME].ToString() ?? string.Empty;

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
    (string match, int score) ICountryCodeVocabulary.MatchCountryName(string countryname, int iMinScore)
       => NameUtilities.FuzzyMatch(countryname, this.m_data.Keys, iMinScore);

    string ICountryCodeVocabulary.CodeToCountrys(string countrycode)
    {
        foreach (string scnane in m_data.Keys)
        {
            MultiLevelKey key = m_data[scnane];
            if ((key != null) && (string.Compare(key.GetField(COL_CODE)!.ToString(false), countrycode, StringComparison.OrdinalIgnoreCase) == 0))
                return scnane;
        }
        return string.Empty;
    }

    string ICountryCodeVocabulary.CountryToCode(string countryname)
    {
        string resolved = NameUtilities.FuzzyMatch(countryname, this.m_data.Keys).BestMatch;

        if (string.IsNullOrWhiteSpace(resolved))
            return string.Empty;

        return m_data.TryGetValue(resolved, out MultiLevelKey? key) ? key.GetField(COL_CODE)!.ToString(false) : string.Empty;
    }
}
