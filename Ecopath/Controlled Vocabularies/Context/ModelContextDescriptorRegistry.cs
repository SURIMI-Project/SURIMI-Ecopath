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
            reg.Register(new KeyFieldDescriptor(ContextFields.ModelName, KeyDomain.Metadata, KeyPurpose.Version, false, 3, MatchStrategy.Exact | MatchStrategy.Fuzzy)
            {
                Kind = FieldKind.Label
            });

            reg.Register(new KeyFieldDescriptor(ContextFields.AreaName, KeyDomain.Metadata, KeyPurpose.SpatialExtent, false, 5, MatchStrategy.Exact | MatchStrategy.Fuzzy)
            {
                Kind = FieldKind.Label
            });

            // Spatial bbox
            reg.Register(new KeyFieldDescriptor(ContextFields.MinLat, KeyDomain.Geographic, KeyPurpose.SpatialExtent, true, 5, MatchStrategy.Exact)
            {
                Kind = FieldKind.Numeric
            });
            reg.Register(new KeyFieldDescriptor(ContextFields.MinLon, KeyDomain.Geographic, KeyPurpose.SpatialExtent, true, 5, MatchStrategy.Exact)
            {
                Kind = FieldKind.Numeric
            });
            reg.Register(new KeyFieldDescriptor(ContextFields.MaxLat, KeyDomain.Geographic, KeyPurpose.SpatialExtent, true, 5, MatchStrategy.Exact)
            {
                Kind = FieldKind.Numeric
            });
            reg.Register(new KeyFieldDescriptor(ContextFields.MaxLon, KeyDomain.Geographic, KeyPurpose.SpatialExtent, true, 5, MatchStrategy.Exact)
            {
                Kind = FieldKind.Numeric
            });

            // Temporal
            reg.Register(new KeyFieldDescriptor(ContextFields.StartYear, KeyDomain.Temporal, KeyPurpose.TimeStamp, true, 6, MatchStrategy.Exact)
            {
                Kind = FieldKind.Numeric
            });
            reg.Register(new KeyFieldDescriptor(ContextFields.EndYear, KeyDomain.Temporal, KeyPurpose.TimeStamp, true, 6, MatchStrategy.Exact)
            {
                Kind = FieldKind.Numeric
            });

            return reg;
        }
    }
}
