using Newtonsoft.Json;
using ControlledVocabularies.Core;

namespace ControlledVocabularies.Vocabularies
{
    public class NERCLifeStageVocabulary
    : ControlledVocabularyBase
    {
        private const string COL_CODE = "ID";
        private const string COL_NAME = "Label";

        public override string VocabularyName => "NERC.S11";
        public override IEnumerable<string> FieldNames => [COL_CODE, COL_NAME];
        public override string CodeFieldName => COL_CODE;
        public override KeyDomain KeyDomain => KeyDomain.Species;
        public override KeyPurpose KeyPurpose => KeyPurpose.Lifestage;

        protected override bool LoadFromSource()
        {
            // Live link, keep around for when reloading will appear:
            // https://vocab.nerc.ac.uk/collection/S11/current/?_profile=nvs&_mediatype=application/ld+json

            string path = @"Includes\NercS11_lifestages.jsonld";

            try
            {
                string json = File.ReadAllText(path);
                dynamic skos = JsonConvert.DeserializeObject(json)!;

                foreach (var concept in skos["@graph"])
                {
                    string id = concept["@id"] ?? "";
                    string label = string.Empty;

                    var bucket = concept["skos:prefLabel"];
                    try
                    {
                        if (bucket != null)
                            label = bucket["@value"] ?? "";
                    }
                    catch (Exception ex)
                    {
                        // Swallow this
                    }

                    if (id.Contains("/S11") && !string.IsNullOrEmpty(label))
                    {
                        var key = new MultiLevelKey();
                        key.SetField(COL_CODE, id);
                        key.SetField(COL_NAME, label);

                        m_data[key.GetField(COL_CODE)!.Value] = key;
                    }
                }
                return m_data.Count > 0;
            }
            catch (Exception ex)
            {
                m_data.Clear();
                return false;
            }
        }
    }
}