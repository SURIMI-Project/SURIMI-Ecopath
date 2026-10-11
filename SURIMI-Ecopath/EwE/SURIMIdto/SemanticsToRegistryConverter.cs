using Eii.ControlledVocabularies.Common;
using Eii.SemanticRegistry.Core;
using Eii.SemanticRegistry.DTO;
using Eii.SemanticRegistry.Serialization;

namespace SURIMIdto
{
    public class SemanticsToRegistryConverter
    {
        public static DomainSemanticRegistryDTO ToRegistryDTO(SemanticsFileDto source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new DomainSemanticRegistryDTO();

            foreach (SemanticsDomainDto domain in source.Domains)
            {
                if (!Enum.TryParse(domain.Domain, true, out KeyDomain domainParsed) ||
                    !Enum.IsDefined(typeof(KeyDomain), domainParsed))
                {
                    throw new InvalidDataException($"Invalid semantic domain: {domain.Domain}");
                }

                var registry = new SemanticRegistryDTO
                {
                    Domain = domainParsed
                };

                foreach (var mapping in domain.Mappings)
                {
                    registry.Registry.Mappings.Add(new CanonicalMappingDTO
                    {
                        SourceCanonical = mapping.Source,
                        TargetCanonical = mapping.Target
                    });
                }

                result.Registries.Add(domainParsed, registry);
            }

            return result;
        }

        public static DomainSemanticRegistry ToRegistry(SemanticsFileDto source)
        {
            return ToRegistryDTO(source).FromDTO();
        }
    }
}
