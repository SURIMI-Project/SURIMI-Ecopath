using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Vocabularies.Country;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;

namespace Ecopath.Services
{
    /// <inheritdoc />
    public class VocabulariesRegisterService : IVocabulariesRegisterService
    {
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly IVocabularyRegistry m_vocabularies;
        private readonly ASFISSpeciesCodeVocabulary m_asfisVocabulary;
        private readonly ISSCFGGearCodeVocabulary m_iSSCFGGearCodeVocabulary;
        private readonly ISO3166CountryCodeVocabulary m_iSO3166CountryCodeVocabulary;
        private readonly SURIMILifestageVocabulary m_SURIMILifestageVocabulary;

        public VocabulariesRegisterService(IVocabularyRegistry vocabularies, IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry, ASFISSpeciesCodeVocabulary asfisVocabulary, ISSCFGGearCodeVocabulary iSSCFGGearCodeVocabulary, ISO3166CountryCodeVocabulary iSO3166CountryCodeVocabulary, SURIMILifestageVocabulary sURIMILifestageVocabulary)
        {
            m_vocabularies = vocabularies;
            m_keyFieldDescriptorRegistry = keyFieldDescriptorRegistry;
            m_asfisVocabulary = asfisVocabulary;
            m_iSSCFGGearCodeVocabulary = iSSCFGGearCodeVocabulary;
            m_iSO3166CountryCodeVocabulary = iSO3166CountryCodeVocabulary;
            m_SURIMILifestageVocabulary = sURIMILifestageVocabulary;
        }

        /// <inheritdoc />
        public void RegisterVocabularies()
        {
            // Register the different species fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.SpeciesCode, KeyDomain.Species, KeyPurpose.Species, FieldKind.Code, true, 10));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Lifestage, KeyDomain.Species, KeyPurpose.Lifestage, FieldKind.Label, false, 3));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Length, KeyDomain.Species, KeyPurpose.Length, FieldKind.Label, false, 3));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Age, KeyDomain.Species, KeyPurpose.Age, FieldKind.Label, false, 3));

            // Register the different gear fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(FishingFields.GearCode, KeyDomain.FleetSegment, KeyPurpose.Gear, FieldKind.Code, true, 10));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(FishingFields.CountryCode, KeyDomain.FleetSegment, KeyPurpose.Country, FieldKind.Code, false, 3));

            // Register the different market fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(MarketFields.MarketCode, KeyDomain.FleetSegment, KeyPurpose.Market, FieldKind.Code, true, 10));

            // Register available look-up vocabularies
            m_vocabularies.Register(m_asfisVocabulary);
            m_vocabularies.Register(m_SURIMILifestageVocabulary);
            m_vocabularies.Register(m_iSSCFGGearCodeVocabulary);
            m_vocabularies.Register(m_iSO3166CountryCodeVocabulary);
        }
    }
}
