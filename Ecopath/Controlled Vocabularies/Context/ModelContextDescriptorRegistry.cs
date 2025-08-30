using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;

namespace ControlledVocabularies.Context
{
    /// <summary>
    /// Registers KeyFieldDescriptors for ModelContext fields so FieldDomain/Purpose work.
    /// </summary>
    public static class ModelContextDescriptorRegistry
    {
        public static KeyFieldDescriptorRegistry Create()
        {
            var reg = new KeyFieldDescriptorRegistry();

            // Identity
            reg.Register(new KeyFieldDescriptor(ContextFields.ModelName, KeyDomain.Metadata, KeyPurpose.Version, FieldKind.Label, false, 3, MatchStrategy.Exact | MatchStrategy.Fuzzy));
            reg.Register(new KeyFieldDescriptor(ContextFields.AreaName, KeyDomain.Metadata, KeyPurpose.SpatialExtent, FieldKind.Label, false, 5, MatchStrategy.Exact | MatchStrategy.Fuzzy));

            // Spatial bbox
            reg.Register(new KeyFieldDescriptor(ContextFields.MinLat, KeyDomain.Geographic, KeyPurpose.SpatialExtent, FieldKind.Numeric, true, 5, MatchStrategy.Exact));
            reg.Register(new KeyFieldDescriptor(ContextFields.MinLon, KeyDomain.Geographic, KeyPurpose.SpatialExtent, FieldKind.Numeric, true, 5, MatchStrategy.Exact));
            reg.Register(new KeyFieldDescriptor(ContextFields.MaxLat, KeyDomain.Geographic, KeyPurpose.SpatialExtent, FieldKind.Numeric, true, 5, MatchStrategy.Exact));
            reg.Register(new KeyFieldDescriptor(ContextFields.MaxLon, KeyDomain.Geographic, KeyPurpose.SpatialExtent, FieldKind.Numeric, true, 5, MatchStrategy.Exact));

            // Temporal
            reg.Register(new KeyFieldDescriptor(ContextFields.StartYear, KeyDomain.Temporal, KeyPurpose.TimeStamp, FieldKind.Numeric, true, 6, MatchStrategy.Exact));
            reg.Register(new KeyFieldDescriptor(ContextFields.EndYear, KeyDomain.Temporal, KeyPurpose.TimeStamp, FieldKind.Numeric, true, 6, MatchStrategy.Exact));

            return reg;
        }
    }
}
