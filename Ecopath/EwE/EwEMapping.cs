using Ecopath.EwE.Wrapper;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Utils;
using EwECore;
using System.Diagnostics;

public class EwEMapping : MultiLevelKey
{
    public EwEMapping(string key, KeyDomain domain, int index, IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry, float proportion = 1) : base(domain)
    {
        this.Parse(key, keyFieldDescriptorRegistry);
        this.Index = index;
        this.Proportion = proportion;
    }

    public int Index { get; set;  }
    public float Proportion { get; set; }

    public string ToInfoString(IEwECore core)
    {
        cCoreInputOutputBase? item = null;
        
        switch (this.Domain)
        {
            case KeyDomain.Species:
                item = core.get_EcopathGroupInputs(this.Index);
                break;
            case KeyDomain.FleetSegment: case KeyDomain.Market:
                item = core.get_EcopathFleetInputs(this.Index);
                break;
            default:
                Debug.Assert(false);
                break;
        }
        if (item == null)
            return string.Format("INVALID {0} => {1}", this.Index, base.ToString());

        return string.Format("EwE index {0}:\"{1}\" @{2} => {3}", this.Index, item.Name, this.Proportion, base.ToString());

    }

    public override string ToString() => base.ToString();

    /// <summary>
    /// Overridden for using MultiLevelKeys as dictionary keys
    /// </summary>
    public override int GetHashCode() => base.GetHashCode(); // Only use key metadata

    /// <summary>
    /// Overridden for using MultiLevelKeys as dictionary keys
    /// </summary>
    public override bool Equals(object? obj) => base.Equals(obj); // Only use key metadata

}
