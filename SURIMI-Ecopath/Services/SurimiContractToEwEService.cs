using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Eii.BlobStore;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.SemanticRegistry;
using EwECore;
using SURIMI.Datamodel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ecopath.Services
{
    public class SurimiContractToEwEService : ISurimiContractToEwEService
    {
        private readonly ILogger<SurimiContractToEwEService> _logger;
        private readonly ISemanticRegistry _semanticRegistry;
        private readonly IBlobStore _blobStore;
        private readonly IKeyFieldDescriptorRegistry _keyFieldDescriptorRegistry;
        private readonly IMultiLevelKeyFactory _multiLevelKeyFactory;

        public SurimiContractToEwEService(
            ILogger<SurimiContractToEwEService> logger, 
            ISemanticRegistry semanticRegistry,
            IBlobStore blobStore,
            IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry,
            IMultiLevelKeyFactory multiLevelKeyFactory)
        {
            _logger = logger;
            _semanticRegistry = semanticRegistry;
            _blobStore = blobStore;
            _keyFieldDescriptorRegistry = keyFieldDescriptorRegistry;
            _multiLevelKeyFactory = multiLevelKeyFactory;
        }

        public async Task ConvertSurimiContractToEwEAsync(List<EwEMapping> mappings, IEwECore core, SurimiContract surimiContract)
        {
            cEcopathDataStructures ecopathds = core.EcopathDataStructures;
            var fleetNames = ecopathds.FleetName;

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
            SemanticRegistryDto? semanticsFile;
            try
            {
                semanticsFile = JsonSerializer.Deserialize<SemanticRegistryDto>(jsonContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse semantics file '{semanticsFilePath}': {ex.Message}", ex);
            }

            var dto = System.Text.Json.JsonSerializer.Deserialize<SemanticRegistryDto>(jsonContent);

            SemanticRegistrySerializer.FromDto(_semanticRegistry, dto);



            var res = SemanticRegistrySerializer.FromDto(_semanticRegistry, semanticsFile);



            if (semanticsFile?.Mappings == null || semanticsFile.Mappings.Count == 0)
            {
                throw new Exception($"Semantics file '{semanticsFilePath}' contains no mappings");
            }

            _logger.LogInformation("Processing {Count} semantic mappings", semanticsFile.Mappings.Count);

            // Process each mapping entry
            foreach (var entry in semanticsFile.Mappings)
            {
                if (string.IsNullOrWhiteSpace(entry.Source) || string.IsNullOrWhiteSpace(entry.Target))
                {
                    _logger.LogWarning("Skipping mapping with empty Source or Target");
                    continue;
                }

                // Determine KeyDomain by inspecting Source field names
                KeyDomain domain = DetermineKeyDomain(entry.Source);
                if (domain == KeyDomain.NotSet)
                {
                    _logger.LogWarning("Could not determine KeyDomain for Source: {Source}", entry.Source);
                    continue;
                }

                // Parse Target to extract index and name
                if (!TryParseTarget(entry.Target, out int index, out string? targetName))
                {
                    _logger.LogWarning("Failed to parse Target: {Target}", entry.Target);
                    continue;
                }

                // Verify fleet name matches (resilient - log warning but continue)
                if (index > 0 && index < fleetNames.Length)
                {
                    string actualFleetName = fleetNames[index];
                    if (!string.Equals(actualFleetName, targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Fleet name mismatch at index {Index}: semantics file has '{TargetName}', EwE has '{ActualName}'",
                            index, targetName, actualFleetName);
                    }
                }
                else
                {
                    _logger.LogWarning("Fleet index {Index} is out of range (max: {Max})", index, fleetNames.Length - 1);
                }

                // Create EwEMapping and add to mappings list
                var mapping = new EwEMapping(entry.Source, domain, index, _keyFieldDescriptorRegistry);
                mappings.Add(mapping);
                _logger.LogDebug("Added mapping: {Source} -> Fleet index {Index} ({Domain})", entry.Source, index, domain);

                // Populate SemanticRegistry with MultiLevelKey pairs
                try
                {
                    var sourceMLK = _multiLevelKeyFactory.FromString(entry.Source, domain, _keyFieldDescriptorRegistry);
                    var targetMLK = _multiLevelKeyFactory.FromString(entry.Target, KeyDomain.NotSet, _keyFieldDescriptorRegistry);
                    _semanticRegistry.Set(sourceMLK, targetMLK);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add semantic registry entry for Source: {Source}", entry.Source);
                }
            }

            _logger.LogInformation("Successfully loaded {Count} fleet mappings from semantics file", mappings.Count);
        }

        /// <summary>
        /// Determines the KeyDomain by inspecting field names in the source key string.
        /// </summary>
        private KeyDomain DetermineKeyDomain(string source)
        {
            string sourceLower = source.ToLowerInvariant();

            // Check for countrycode -> FleetSegment
            if (sourceLower.Contains("countrycode"))
                return KeyDomain.FleetSegment;

            // Check for marketcode -> Market
            if (sourceLower.Contains("marketcode"))
                return KeyDomain.Market;

            return KeyDomain.NotSet;
        }

        /// <summary>
        /// Parses the Target key string to extract the index and name fields.
        /// Expected format: "model=fleet;name=Bottom trawl Spain;index=1"
        /// </summary>
        private bool TryParseTarget(string target, out int index, out string? name)
        {
            index = 0;
            name = null;

            var parts = target.Split(';');
            foreach (var part in parts)
            {
                var kvp = part.Split('=', 2);
                if (kvp.Length != 2)
                    continue;

                string key = kvp[0].Trim().ToLowerInvariant();
                string value = kvp[1].Trim();

                if (key == "index")
                {
                    if (!int.TryParse(value, out index))
                        return false;
                }
                else if (key == "name")
                {
                    name = value;
                }
            }

            return index > 0; // Valid index is required
        }

        // JSON DTOs for deserializing the semantics file
        private record SemanticsFile
        {
            [JsonPropertyName("Mappings")]
            public List<SemanticsEntry> Mappings { get; set; } = new();
        }

        private record SemanticsEntry
        {
            [JsonPropertyName("Source")]
            public string Source { get; set; } = string.Empty;

            [JsonPropertyName("Target")]
            public string Target { get; set; } = string.Empty;
        }
    }
}
