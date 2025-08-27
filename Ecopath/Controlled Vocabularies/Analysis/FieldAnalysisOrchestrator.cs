using ControlledVocabularies.Analysis;
using ControlledVocabularies.Analysis.Strategies;
using ControlledVocabularies.Core;

/// <summary>
/// Orchestrates multiple analysis strategies
/// </summary>
public class FieldAnalysisOrchestrator
{
    private readonly List<IFieldAnalysisStrategy> _strategies = new();

    public FieldAnalysisOrchestrator()
    {
        // Register strategies in priority order
        RegisterStrategy(new CodeNamePairStrategy());
        RegisterStrategy(new HierarchicalStructureStrategy());
        RegisterStrategy(new RepetitiveMeaningfulStrategy());
        RegisterStrategy(new BasicStatisticsStrategy()); // Fallback
        // ... more strategies
    }

    public void RegisterStrategy(IFieldAnalysisStrategy strategy) => _strategies.Add(strategy);

    public CompositeAnalysisResult AnalyzeField(string fieldName, List<string> sampleValues,
        IEnumerable<MultiLevelKey> allRecords, AnalysisContext context)
    {
        var results = new List<FieldAnalysisResult>();

        // Run strategies in priority order
        foreach (var strategy in _strategies.OrderByDescending(s => s.Priority))
        {
            var result = strategy.Analyze(fieldName, sampleValues, allRecords, context);
            if (result.HasSuggestion)
            {
                results.Add(result);
            }
        }

        return new CompositeAnalysisResult(fieldName, results);
    }
}