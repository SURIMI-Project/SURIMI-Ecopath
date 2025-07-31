/// <summary>
/// Describes the parsing properties of multi-level key fields.
/// </summary>
public class KeyFieldDescriptor
{
    public string FieldName { get; }
    public bool IsRequired { get; }
    public int Weight {  get; }
    public KeyPurpose Purpose { get; }
    public KeyDomain Domain { get; }

    public KeyFieldDescriptor(string fieldName, bool isRequired = false, int weight = 1, KeyDomain domain = KeyDomain.Species, KeyPurpose purpose = KeyPurpose.NotSet)
    {
        FieldName = fieldName;
        IsRequired = isRequired;
        Weight = weight;
        Purpose = purpose;
        Domain = domain;
    }
}
