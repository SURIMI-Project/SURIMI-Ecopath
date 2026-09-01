using Ecopath.EwE.Wrapper;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using EwECore;
using System.Diagnostics;

namespace Ecopath.EwE
{
    public class EwEMapping : MultiLevelKey
    {
        /// <summary>
        /// Map a multilevel <paramref name="key"/> to EwE item of type <paramref name="domain"/> and index <paramref name="index"/>,
        /// using contextual field names predefined in <paramref name="keyFieldDescriptorRegistry"/> with optional proportion <paramref name="proportion"/>.
        /// In fact, propoertion should most likely not be part of this setup, as it can fluctuate.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="domain"></param>
        /// <param name="index"></param>
        /// <param name="keyFieldDescriptorRegistry"></param>
        /// <param name="proportion"></param>
        public EwEMapping(string key, KeyDomain domain, int index, IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry, float proportion = 1) : base(domain)
        {
            this.Index = index;
            this.Proportion = proportion;

            this.Parse(key, keyFieldDescriptorRegistry);
        }

        public int Index { get; set; }
        public float Proportion { get; set; }

        public string ToInfoString(IEwECore core)
        {
            cCoreInputOutputBase? item = null;

            switch (this.Domain)
            {
                case KeyDomain.Species:
                    item = core.get_EcopathGroupInputs(this.Index);
                    break;
                case KeyDomain.FleetSegment:
                case KeyDomain.Market:
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
}