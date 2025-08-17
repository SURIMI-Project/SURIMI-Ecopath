/// <summary>
/// Field-specific descriptors, organized per <see cref="KeyDomain"/>.
/// </summary>
public class KeyFieldDescriptorRegistry
{
    private readonly Dictionary<KeyDomain, List<KeyFieldDescriptor>> m_descriptors = new();

    public void Register(KeyDomain domain, KeyFieldDescriptor descriptor)
    {
        if (!m_descriptors.TryGetValue(domain, out var descriptors))
            m_descriptors[domain] = descriptors = new List<KeyFieldDescriptor>();
        descriptors.Add(descriptor);
        // For now, set this via the registry. descriptor field may disappear entirely
        descriptor.Domain = domain;
    }

    public IEnumerable<KeyFieldDescriptor> Get(KeyDomain domain) =>
        m_descriptors.TryGetValue(domain, out var matcher) ? matcher : new List<KeyFieldDescriptor>();
}

