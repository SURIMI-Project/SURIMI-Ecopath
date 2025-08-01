using System.Data;
using System.Reflection;
using System.Text;

/// <summary>
/// Represents a multi-level, self-describing key (e.g., for species or fleets)
/// </summary>
public class MultiLevelKey
{
    #region Private parts 

    private Dictionary<string, MultiLevelKeyField> Fields { get; set; } = new();
    public KeyDomain Domain { get; set; }
    public DateTime TimeStamp { get; set; } = DateTime.MinValue;

    #endregion // Private parts (tee hee hee)

    public static MultiLevelKey FromObject(object source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var fields = new Dictionary<string, MultiLevelKeyField>();

        var props = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (prop != null)
            {
                if (prop.CanRead && prop.PropertyType == typeof(string))
                {
                    var valueObj = prop.GetValue(source);
                    if (valueObj is string value && !string.IsNullOrWhiteSpace(value))
                    {
                        var key = MultiLevelKeyField.FromString(value);
                        if (key != null)
                            fields[ToSafeKey(prop.Name)] = key;
                    }
                }
            }
        }

        return new MultiLevelKey() { Fields = fields };
    }

    public T? ToObject<T>(bool includeVocabulary = true)
    {
        var fields = new Dictionary<string, string>();

        var obj = Activator.CreateInstance(typeof(T));
        if (obj != null)
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop != null)
                {
                    if (prop.CanWrite && prop.PropertyType == typeof(string))
                    {
                        MultiLevelKeyField? val = null;
                        if (Fields.TryGetValue(ToSafeKey(prop.Name), out val))
                            prop.SetValue(obj, val.ToString(includeVocabulary));
                        else
                            prop.SetValue(obj, string.Empty);
                    }
                }
            }
        }
        return (T?)obj;
    }

    public static MultiLevelKey FromDataRow(DataRow source)
    {
        var fields = new Dictionary<string, MultiLevelKeyField>();
        var dt = source.Table;

        foreach (DataColumn col in dt.Columns)
        {
            var valueObj = source[col];
            if (valueObj is string value && !string.IsNullOrWhiteSpace(value))
            {
                var key = MultiLevelKeyField.FromString(value);
                if (key != null)
                    fields[ToSafeKey(col.ColumnName)] = key;
            }
        }
        return new MultiLevelKey() { Fields = fields };
    }

    public static MultiLevelKey FromString(string value)
    {
        var k = new MultiLevelKey();
        k.Parse(value);
        return k;
    }

    public bool Parse(string keyStr)
    {
        if (string.IsNullOrWhiteSpace(keyStr)) return false;

        foreach (var kvpair in keyStr.Split(';'))
        {
            var parts = kvpair.Split('=');
            if (parts.Length == 2)
            {
                this.SetField(parts[0], parts[1]);
            }
        }
        return true;
    }

    public void SetField(string key, string value, bool bRemoveVocabulary = false)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        key = ToSafeKey(key);

        if (string.IsNullOrWhiteSpace(value))
        {
            this.Fields.Remove(key);
            return;
        }

        int iSep = value.IndexOf(':');
        string vocab = (iSep == -1 | bRemoveVocabulary) ? string.Empty : value.Substring(0, iSep);
        value = (iSep == -1) ? value: value.Substring(iSep + 1);

        this.Fields[ToSafeKey(key)] = new MultiLevelKeyField(value, vocab);
    }

    public MultiLevelKeyField? GetField(string key)
    {
        key = ToSafeKey(key);
        if (this.Fields.TryGetValue(key, out var value)) return value;
        return null;
    }

    public IEnumerable<string> FieldNames =>this.Fields.Keys;

    public IEnumerable<MultiLevelKeyField> FieldValues => this.Fields.Values;

    /// <summary>
    /// Returns a canonical string representation of the key
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append(string.Join(";",this.Fields.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")));
        sb.Append($";domain={this.Domain.ToString()}");
        if (this.TimeStamp > DateTime.MinValue)
            sb.Append($"timestamp={this.TimeStamp.ToShortDateString()}");
        return sb.ToString().ToLowerInvariant();
    }

    private static string ToSafeKey(string key)
    {
        return key.Trim().ToLowerInvariant();
    }
}