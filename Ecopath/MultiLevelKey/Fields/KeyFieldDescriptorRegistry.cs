/// <summary>
/// Field-specific descriptors, organized per <see cref="KeyDomain"/>.
/// </summary>
public class KeyFieldDescriptorRegistry
{
    private readonly Dictionary<(KeyDomain, string), KeyFieldDescriptor> Descriptors = new();

    public void Register(KeyFieldDescriptor descriptor)
    {
        var key = (descriptor.Domain, descriptor.FieldName);
        Descriptors[key] = descriptor;
    }

    public KeyFieldDescriptor? Get(KeyDomain domain, string fieldName)
    {
        Descriptors.TryGetValue((domain, fieldName), out var descriptor);
        return descriptor;
    }

    public IEnumerable<KeyFieldDescriptor> GetAll(KeyDomain domain)
    {
        return Descriptors
            .Where(kvp => kvp.Key.Item1 == domain)
            .Select(kvp => kvp.Value);
    }
 }