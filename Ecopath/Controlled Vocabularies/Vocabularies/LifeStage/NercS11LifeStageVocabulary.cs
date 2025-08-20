using ControlledVocabularies.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ControlledVocabularies.Vocabularies
{
    public class NERCLifeStageVocabulary
    : ControlledVocabularyBase
    {
        private const string COL_CODE = "ID";
        private const string COL_LABEL = "Label";

        public override string VocabularyName => "NERC.S11";
        public override string CodeFieldName => COL_CODE;
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Lifestage;

        protected override bool LoadFromSource()
        {
            // Live link, keep around for when reloading will appear:
            // https://vocab.nerc.ac.uk/collection/S11/current/?_profile=nvs&_mediatype=application/ld+json

            string path = @"Includes\NercS11_lifestages.jsonld";

            AddField(COL_CODE, Domain, Purpose, isRequired: true, weight: 1, strategy: MatchStrategy.Exact);
            AddField(COL_LABEL, Domain, Purpose, isRequired: true, weight: 1, strategy: MatchStrategy.Exact | MatchStrategy.Fuzzy);

            var root = JObject.Parse(File.ReadAllText(path));
            var graph = (JArray?)root["@graph"] ?? new JArray();

            foreach (var node in graph.OfType<JObject>())
            {
                var type = node["@type"]?.ToString();
                if (type is null || !type.Contains("Concept", StringComparison.OrdinalIgnoreCase)) continue;

                var id = node["@id"]?.ToString() ?? "";
                if (!id.Contains("/S11", StringComparison.OrdinalIgnoreCase)) continue;

                // label can be object or array
                string label = "";
                var pref = node["skos:prefLabel"];
                if (pref is JObject o)
                    label = o["@value"]?.ToString() ?? "";
                else if (pref is JArray arr)
                    label = arr.OfType<JObject>()
                               .FirstOrDefault(x => string.Equals(x["@language"]?.ToString(), "en", StringComparison.OrdinalIgnoreCase))
                              ?["@value"]?.ToString()
                           ?? arr.OfType<JObject>().FirstOrDefault()?["@value"]?.ToString()
                           ?? "";

                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(label))
                    AddRow(id, label);
            }
            return true;
        }

        private void AddRow(string code, string label)
        {
            var drow = this.Table.NewRow();
            drow[COL_CODE] = code;
            drow[COL_LABEL] = label;
            this.Table.Rows.Add(drow);
        }
    }
}