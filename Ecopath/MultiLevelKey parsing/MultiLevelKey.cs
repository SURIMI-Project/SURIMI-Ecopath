using System.Reflection;

/// <summary>
/// Represents a multi-level, self-describing key (e.g., for species or fleets)
/// </summary>
public class MultiLevelKey
{

    // ToDo: enforce lowercase field names through property access; hide dictionary

    private Dictionary<string, string> Fields { get; set; } = new();
    public KeyDomain Domain { get; set; }

    public static MultiLevelKey FromObject(object source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var fields = new Dictionary<string, string>();

        var props = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (prop != null)
            {
                if (prop.CanRead && prop.PropertyType == typeof(string))
                {
                    var valueObj = prop.GetValue(source);
                    if (valueObj is string value && !string.IsNullOrWhiteSpace(value))
                        fields[ToSafeKey(prop.Name)] = value;
                }
            }
        }

        return new MultiLevelKey() { Fields = fields };
    }

    public T? ToObject<T>()
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
                        string? val = "";
                        if (Fields.TryGetValue(ToSafeKey(prop.Name), out val))
                            prop.SetValue(obj, val);
                        else
                            prop.SetValue(obj, string.Empty);

                    }
                }
            }
        }
        return (T?)obj;
    }

    public bool Parse(string keyStr)
    {
        if (string.IsNullOrWhiteSpace(keyStr)) return false;

        foreach (var kvpair in keyStr.Split(';'))
        {
            var parts = kvpair.Split('=');
            if (parts.Length == 2)
            {
                if (parts[1].Contains(':'))
                {
                    // For now remove standard classifiers
                    parts[1] = parts[1].Substring(parts[1].IndexOf(':') + 1);
                }
                this.SetField(parts[0], parts[1]);
            }
        }
        return true;
    }

    public void SetField(string key, string value, bool bRemoveVocabulary = true)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            this.Fields.Remove(key);
            return;
        }

        if (bRemoveVocabulary) 
            value= value.Substring(value.IndexOf(':') + 1);

        this.Fields[ToSafeKey(key)] = value;
    }

    public string GetField(string key)
    {
        key = ToSafeKey(key);
        if (this.Fields.TryGetValue(key, out var value)) return value;
        return string.Empty;
    }

    public IEnumerable<string> FieldNames() =>this.Fields.Keys;

    /// <summary>
    /// Returns a canonical string representation of the key
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return string.Join(";",
            this.Fields.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))
            + $";domain={this.Domain.ToString().ToLowerInvariant()}";
    }

    private static string ToSafeKey(string key)
    {
        return key.Trim().ToLowerInvariant();
    }
}