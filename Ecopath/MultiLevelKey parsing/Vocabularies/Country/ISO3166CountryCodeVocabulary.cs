using CsvHelper;
using System.Data;
using System.Globalization;
using Utilities;

public class ISO3166CountryCodeVocabulary : ICountryCodeVocabulary
{
    private Dictionary<string, string> m_keys = new();

    KeyDomain IControlledVocabulary.KeyDomain => KeyDomain.Country;

    string IControlledVocabulary.VocabularyName => "ISO-3166";

    public bool Load()
    {
        if (m_keys.Count > 0)
            return true;

        string fin = @"Includes\ISO-3166-Countries-with-Regional-Codes.csv";
        using (var reader = new StreamReader(fin))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            using (var dr = new CsvDataReader(csv))
            {
                DataTable dt = new();
                dt.Columns.Add("Alpha3_Code", typeof(string));
                dt.Columns.Add("Country_Name", typeof(string));

                try
                {
                    dt.Load(dr);
                    foreach (DataRow dataRow in dt.Rows)
                    {
                        string code = (string)dataRow["Alpha3_Code"];
                        string scname = (string)dataRow["Country_Name"];

                        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(scname))
                            m_keys[NameUtilities.NormalizeName(scname)] = code;
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
    (string match, int score) ICountryCodeVocabulary.MatchCountryName(string countryname, int iMinScore)
       => NameUtilities.FuzzyMatch(countryname, this.m_keys.Keys, iMinScore);

    string ICountryCodeVocabulary.CodeToCountrys(string countrycode)
    {
        foreach (string key in m_keys.Keys)
            if (string.Compare(m_keys[key], countrycode, StringComparison.OrdinalIgnoreCase) == 0)
                return key;
        return string.Empty;
    }

    string ICountryCodeVocabulary.CountryToCode(string countryname)
    {
        string resolved = NameUtilities.FuzzyMatch(countryname, this.m_keys.Keys).BestMatch;

        if (string.IsNullOrWhiteSpace(resolved))
            return string.Empty;

        return m_keys.TryGetValue(resolved, out var code) ? code : string.Empty;
    }
}
