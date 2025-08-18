using CsvHelper;
using System.Data;
using System.Globalization;

public class ISO3166CountryCodeVocabulary 
    : ControlledVocabularyBase
{
    /// <todo>Use a live online source, with a local version as backup. Need some future smarts here</todo>
    private const string FileName = @"Includes\ISO-3166-Countries-with-Regional-Codes.csv";
    private const string COL_CODE = "alpha-3";
    private const string COL_NAME = "name";

    public override string VocabularyName => "ISO-3166";
    public override IEnumerable<string> FieldNames => [COL_CODE, COL_NAME];
    public override string CodeFieldName => COL_CODE;
    public override KeyDomain KeyDomain => KeyDomain.Country;
    public override KeyPurpose KeyPurpose => KeyPurpose.Country;

    protected override bool LoadFromSource()
    {
        if (m_data.Count > 0)
            return true;

        using var reader = new StreamReader(FileName);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        using var dr = new CsvDataReader(csv);

        DataTable dt = new();
        dt.Columns.Add(COL_CODE, typeof(string));
        dt.Columns.Add(COL_NAME, typeof(string));

        try
        {
            dt.Load(dr);
            foreach (DataRow dataRow in dt.Rows)
            {
                var key = MultiLevelKey.FromDataRow(dataRow);
                string code = key.GetField(COL_CODE)!.Value;
                string scname = key.GetField(COL_NAME)!.Value;

                if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(scname))
                {
                    m_data[code] = key;
                }
                else
                {
                    // ToDo: log omission
                }
            }

        }
        catch (Exception ex)
        {
            // ToDo: log ex.Message or ex.ToString()
            m_data.Clear();
            return false;
        }

        return (m_data.Count > 0);
    }
}
