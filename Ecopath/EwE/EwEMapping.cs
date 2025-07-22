public class EwEMapping : MultiLevelKey
{
    public EwEMapping() 
    { 
        // NOP;
    }

    public EwEMapping(string key, KeyDomain domain, int index, float proportion = 1) : this()
    {
        this.Parse(key);
        this.Domain = domain;
        this.Index = index;
        this.Proportion = proportion;
    }
    public int Index { get; set;  }
    public float Proportion { get; set; }

    public override string ToString() => base.ToString(); // Only use key metadata
    public override int GetHashCode() => base.GetHashCode(); // Only use key metadata
    public override bool Equals(object? obj) => base.Equals(obj); // Only use key metadata

}
