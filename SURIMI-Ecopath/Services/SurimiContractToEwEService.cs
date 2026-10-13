using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Eii.BlobStore;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.SemanticRegistry.Core;
using EwECore;
using SURIMI.Datamodel;
using SURIMIdto;
using System.Text.Json;

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

        public async Task ReadSemanticMappings(List<EwEMapping> mappings, IEwECore core, string scenarioName)
        {
            cEcopathDataStructures ecopathds = core.EcopathDataStructures;

            // Read the semantics file from blob store
            string semanticsFilePath = $"{scenarioName}.semantics";
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
            SemanticsFileDto? semanticsFile;
            try
            {
                semanticsFile = JsonSerializer.Deserialize<SemanticsFileDto>(jsonContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse semantics file '{semanticsFilePath}': {ex.Message}", ex);
            }

            if (semanticsFile == null)
            {
                throw new InvalidDataException($"Semantics file '{semanticsFilePath}' contains no valid data.");
            }

            IDomainSemanticRegistry registry = SemanticsToRegistryConverter.ToRegistry(semanticsFile!);
            _logger.LogInformation($"Processing {registry.Domains.Count()} semantic domains");

            foreach (KeyDomain semanticsDomain in registry.Domains)
            {
                _logger.LogInformation("Processing domain: {Domain}", semanticsDomain);
                var dbids = semanticsDomain switch
                {
                    KeyDomain.FleetSegment => ecopathds.FleetDBID,
                    KeyDomain.Market => ecopathds.FleetDBID,
                    KeyDomain.Species => ecopathds.GroupDBID,
                    _ => Array.Empty<int>()
                };

                ISemanticRegistry semregistry = registry.GetRegistry(semanticsDomain);
                // Process each mapping entry
                foreach (IMultiLevelKey source in semregistry.GetAllSources())
                {
                    if (!semregistry.TryGet(source, out IMultiLevelKey target))
                    {
                        throw new InvalidOperationException($"Unable to resolve semantic mapping for '{source}' in domain '{semanticsDomain}'.");
                    }

                    // Parse Target to extract index and name
                    if (!int.TryParse(target.GetField("dbid")?.Value, out int dbid))
                    {
                        throw new InvalidDataException($"Missing or invalid DBID in semantic target '{target}'.");
                    }

                    int index = Array.IndexOf(dbids, dbid);
                    if (index == -1)
                    {
                        _logger.LogWarning($"Target '{target}' not found in {semanticsDomain} domain");
                        continue;
                    }

                    // Create EwEMapping and add to mappings list
                    var mapping = new EwEMapping(source, semanticsDomain, index, _keyFieldDescriptorRegistry);
                    mappings.Add(mapping);
                    _logger.LogDebug($"Added mapping: {source} -> Index {index} ({semanticsDomain})");
                }
            }
            _logger.LogInformation($"Successfully loaded {mappings.Count} mappings from semantics file");
        }
    }
}
