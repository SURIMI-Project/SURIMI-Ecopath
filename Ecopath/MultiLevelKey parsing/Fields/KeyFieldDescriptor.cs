public class KeyFieldDescriptor
{
    public string FieldName { get; }
    public bool IsRequired { get; }
    public int Weight {  get; }

    public KeyFieldDescriptor(string fieldName, bool isRequired = false, int weight = 1)
    {
        FieldName = fieldName;
        IsRequired = isRequired;
        Weight = weight;
    }
}
