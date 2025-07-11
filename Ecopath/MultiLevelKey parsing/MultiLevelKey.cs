using System.Reflection;

public enum KeyDomain
{
    Species,
    FleetSegment,
    Market
}

/// <summary>
/// Represents a multi-level, self-describing key (e.g., for species or fleets)
/// </summary>
public class MultiLevelKey
{
    public Dictionary<string, string> Fields { get; set; } = new();
    public KeyDomain Domain { get; set; }
    public int Index { get; set; }
    public float Propertion { get; set; }

    public DateTime? Timestamp { get; set; }
    
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
                        fields[prop.Name.ToLower()] = value;
                }
            }
        }

        return new MultiLevelKey() { Fields = fields };
    }

    public T? ToObject<T>()
    {
        var fields = new Dictionary<string, string>();

        var obj = Activator.CreateInstance(typeof(T));
        if (obj  != null) 
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop != null)
                {
                    if (prop.CanWrite && prop.PropertyType == typeof(string))
                        prop.SetValue(obj, Fields.TryGetValue(prop.Name, out var value) ? value : string.Empty);
                }

            }
        }
        return (T?)obj;
    }

    public static MultiLevelKey Parse(string keyStr, KeyDomain domain, int iIndex, float proportion = 1)
    {
        var key = new MultiLevelKey()
        {
            Domain = domain,
            Index = iIndex,
            Propertion = proportion
        };

        if (string.IsNullOrWhiteSpace(keyStr)) return key;

        foreach (var kvpair in keyStr.Split(';'))
        {
            var parts = kvpair.Split('=');
            if (parts.Length == 2)
                key.Fields[parts[0].Trim()] = parts[1].Trim();
        }
        return key;
    }

    public override string ToString()
    {
        return string.Join("; ", Fields.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}