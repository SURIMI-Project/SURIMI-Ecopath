using CsvHelper;
using Ecopath.Utilities;
using System.Data;
using System.Globalization;

public class ASFISSpeciesCodeVocabulary 
    : ControlledVocabularyBase
{
    private const string COL_CODE = "Alpha3_Code";
    private const string COL_NAME = "Scientific_Name";

    public override string VocabularyName => "ASFIS";
    public override KeyDomain KeyDomain => KeyDomain.Species;
    public override KeyPurpose KeyPurpose => KeyPurpose.Species;

    public override IEnumerable<string> FieldNames => [COL_CODE, COL_NAME];
    public override string CodeFieldName => COL_CODE;

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
                            m_data[StringHelpers.NormalizeName(scname)] = key;
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
}
