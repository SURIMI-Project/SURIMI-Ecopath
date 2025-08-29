using System;
using System.Globalization;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;

namespace ControlledVocabularies.Context
{
    /// <summary>
    /// Shared “context is everything” object, backed by a MultiLevelKey.
    /// Stores model scope in schema-normalized fields with proper descriptors.
    /// </summary>
    public sealed class ModelContext
    {
        private readonly MultiLevelKey _key;

        public ModelContext(IKeyFieldDescriptorRegistry? registry = null)
        {
            _key = MultiLevelKey.FromPairs(Array.Empty<(string, string?)>(), KeyDomain.Metadata, registry, strict: false);
        }

        public MultiLevelKey Key => _key; // expose for resolvers/indexers needing raw MLK

        public static ModelContext? Empty => new ModelContext() {}; 

        // ---- Setters (explicit & legible) ----

        public void SetModelName(string name)
        {
            _key.SetField(ContextFields.ModelName, name);
        }

        public void SetAreaName(string area)
        {
            _key.SetField(ContextFields.AreaName, area);
        }

        public void SetBoundingBox(double minLat, double minLon, double maxLat, double maxLon)
        {
            // Store as culture-invariant strings; FieldKind.Numeric normalization handles dot decimal.
            _key.SetField(ContextFields.MinLat, minLat.ToString(CultureInfo.InvariantCulture));
            _key.SetField(ContextFields.MinLon, minLon.ToString(CultureInfo.InvariantCulture));
            _key.SetField(ContextFields.MaxLat, maxLat.ToString(CultureInfo.InvariantCulture));
            _key.SetField(ContextFields.MaxLon, maxLon.ToString(CultureInfo.InvariantCulture));
        }

        public void SetYears(int startYear, int endYear)
        {
            _key.SetField(ContextFields.StartYear, startYear.ToString(CultureInfo.InvariantCulture));
            _key.SetField(ContextFields.EndYear, endYear.ToString(CultureInfo.InvariantCulture));
        }

        public void SetTimestamp(DateTime utc)
        {
            _key.TimeStamp = utc;
        }
    }
}
