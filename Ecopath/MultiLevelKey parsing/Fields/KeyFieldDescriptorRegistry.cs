/// <summary>
/// Field-specific descriptors, organized per <see cref="KeyDomain"/>.
/// </summary>
public class KeyFieldDescriptorRegistry
{
    private readonly Dictionary<KeyDomain, List<KeyFieldDescriptor>> m_desscriptors = new();

    public void Register(KeyDomain domain, KeyFieldDescriptor descriptor)
    {
        if (!m_desscriptors.TryGetValue(domain, out var descriptors))
            m_desscriptors[domain] = descriptors = new List<KeyFieldDescriptor>();
        descriptors.Add(descriptor);
    }

    public IEnumerable<KeyFieldDescriptor> Get(KeyDomain domain) =>
        m_desscriptors.TryGetValue(domain, out var matcher) ? matcher : new List<KeyFieldDescriptor>();
}

