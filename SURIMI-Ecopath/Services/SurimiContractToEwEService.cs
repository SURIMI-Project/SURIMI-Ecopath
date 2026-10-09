using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Eii.BlobStore;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using EwECore;
using SURIMI.Datamodel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ecopath.Services
{
    public class SurimiContractToEwEService : ISurimiContractToEwEService
    {
        private readonly ILogger<SurimiContractToEwEService> _logger;
        private readonly IBlobStore _blobStore;
        private readonly IKeyFieldDescriptorRegistry _keyFieldDescriptorRegistry;
        private readonly IMultiLevelKeyFactory _multiLevelKeyFactory;

        public SurimiContractToEwEService(
            ILogger<SurimiContractToEwEService> logger,
            IBlobStore blobStore,
            IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry,
            IMultiLevelKeyFactory multiLevelKeyFactory)
        {
            _logger = logger;
            _blobStore = blobStore;
            _keyFieldDescriptorRegistry = keyFieldDescriptorRegistry;
            _multiLevelKeyFactory = multiLevelKeyFactory;
        }

        public async Task ReadSemanticMappings(List<EwEMapping> mappings, IEwECore core, SurimiContract surimiContract)
        {
            cEcopathDataStructures ecopathds = core.EcopathDataStructures;
            var fleetNames = ecopathds.FleetName;
            var fleetDBIDs = ecopathds.FleetDBID;
            var groupNames = ecopathds.GroupName;
            var groupDBIDs = ecopathds.GroupDBID;

            var factory = new MultiLevelKeyFactory();

            // Read the semantics file from blob store
            string semanticsFilePath = "EwE_SURIMI.semantics";
            _logger.LogInformation("Loading fleet mappings from {SemanticsFile}", semanticsFilePath);

            string jsonContent;
            try
            {
                jsonContent = await _blobStore.ReadAllTextAsync(semanticsFilePath, PathType.Input);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read semantics file '{semanticsFilePath}': {ex.Message}", ex);
            }

            // Deserialize JSON
            SemanticDomainDto? semanticsFile;
            try
            {
                semanticsFile = JsonSerializer.Deserialize<SemanticDomainDto>(jsonContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse semantics file '{semanticsFilePath}': {ex.Message}", ex);
            }

            // JS 9Oct26: pragmatic shortcut to avoid having to create a new SemanticRegistry and populate it with the DTO.
            // This is a temporary measure until we can refactor the code to use the DTO directly.
            if (semanticsFile?.Domains == null || semanticsFile.Domains.Count == 0)
            {
                throw new Exception($"Semantics file '{semanticsFilePath}' contains no mappings");
            }

            _logger.LogInformation("Processing {Count} semantic domains", semanticsFile.Domains.Count);

            foreach (SemanticsDomain semanticsDomain in semanticsFile.Domains)
            {
                _logger.LogInformation("Processing domain: {Domain}", semanticsDomain.Domain);

                if (semanticsDomain.Mappings == null || semanticsDomain.Mappings.Count == 0)
                {
                    _logger.LogWarning("Domain '{Domain}' contains no mappings", semanticsDomain.Domain);
                    continue;
                }

                // Process each mapping entry
                foreach (var entry in semanticsDomain.Mappings)
                {
                    if (string.IsNullOrWhiteSpace(entry.Source) || string.IsNullOrWhiteSpace(entry.Target))
                    {
                        _logger.LogWarning("Skipping mapping with empty Source or Target");
                        continue;
                    }

                    // Parse Target to extract index and name
                    var target = factory.FromString(entry.Target, semanticsDomain.Domain, null);
                    var name = target.GetField("name")?.Value;
                    var dbid = Convert.ToInt16(target.GetField("dbid")?.Value);
                    int index = -1;

                    int[]? ids = null;
                    string[]? names = null;

                    switch (semanticsDomain.Domain)
                    {
                        case KeyDomain.FleetSegment:
                        case KeyDomain.Market:
                            ids = fleetDBIDs;
                            names = fleetNames;
                            break;
                        case KeyDomain.Species:
                            ids = groupDBIDs;
                            names = groupNames;
                            break;
                        default:
                            _logger.LogWarning("Mapping Source '{Source}' has unrecognized domain", entry.Source);
                            break;
                    }

                    index = Array.IndexOf(ids!, dbid);
                    if (index == -1)
                    {
                        _logger.LogWarning("{entry.Target} not found in {domain} domain", entry.Target, semanticsDomain);
                        continue;
                    }

                    // Create EwEMapping and add to mappings list
                    var mapping = new EwEMapping(entry.Source, semanticsDomain.Domain, index, _keyFieldDescriptorRegistry);
                    mappings.Add(mapping);
                    _logger.LogDebug("Added mapping: {Source} -> Index {Index} ({Domain})", entry.Source, index, semanticsDomain);
                }
            }
            _logger.LogInformation("Successfully loaded {Count} mappings from semantics file", mappings.Count);
        }

        // JSON DTOs for deserializing the semantics file
        private sealed class SemanticDomainDto
        {
            [JsonPropertyName("Domains")]
            public List<SemanticsDomain> Domains { get; set; } = new();
        }

        private sealed class SemanticsDomain
        {
            [JsonPropertyName("Domain")]
            public string DomainString { get; set; } = string.Empty;

            [JsonIgnore]
            public KeyDomain Domain
            {
                get
                {
                    if (Enum.TryParse(DomainString, out KeyDomain domain))
                        return domain;
                    return KeyDomain.NotSet;
                }
            }

            [JsonPropertyName("Mappings")]
            public List<SemanticsMapping> Mappings { get; set; } = new();
        }

        private sealed class SemanticsMapping
        {
            [JsonPropertyName("Source")]
            public string Source { get; set; } = string.Empty;

            [JsonPropertyName("Target")]
            public string Target { get; set; } = string.Empty;
        }
    }
}
