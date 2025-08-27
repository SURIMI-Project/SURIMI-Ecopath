/// <summary>
/// Hierarchical nesting analysis for code fields
/// </summary>
public class HierarchicalNestingAnalysis
{
    public bool IsHierarchical { get; set; }
    public int MaxDepth { get; set; }
    public List<char> Separators { get; set; } = new();
    public double ConsistencyRatio { get; set; } // How consistently the hierarchy is used

    public override string ToString() =>
        IsHierarchical
            ? $"Hierarchical (depth: {MaxDepth}, separators: {string.Join(",", Separators)}, consistency: {ConsistencyRatio:F2})"
            : "Non-hierarchical";
}