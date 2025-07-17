/// <summary>
/// <para>Ecospace operates at the FG level, where multiple species may share biomass, 
/// productivity, and dispersal traits. However, external models (like POSEIDON) 
/// require species-level biomass for targeting, behaviour, and market dynamics. 
/// Directly modeling species within EwE would be duplicative and invasive.</para>
/// <para>This class maintains species proportions within FGs and manage their 
/// dynamics outside of the EwE software.</para>
/// </summary>
public class GroupSpeciesProportions
{
    /// <summary>
    /// Group sequential index for debugging purposes.
    /// </summary>
    public readonly int m_iGroup;

    /// <summary>
    /// Fixed recovery rate.
    /// </summary>
    public readonly double m_r;

    /// <summary>
    /// (row, col) → {species → proportion}
    /// </summary>
    private readonly Dictionary<(int row, int col), Dictionary<string, double>> m_proportions = new();

    /// <summary>
    /// Species baseline group proportion (p0)
    /// </summary>
    private readonly Dictionary<string, double> m_baselineProportions = new();

    public GroupSpeciesProportions(int iGroup, double r, IEnumerable<(int row, int col)> activeCells)
    {
        this.m_iGroup = iGroup;
        this.m_r = r;

        foreach (var cell in activeCells)
            m_proportions[cell] = new Dictionary<string, double>();
    }

    #region Public access

    public void SetBaseline(string species, double p0)
    {
        m_baselineProportions[species] = p0;
        foreach (var cell in m_proportions.Values)
            cell[species] = p0;
    }

    public double GetNormalizedProportion(int row, int col, string species)
    {
        if (!m_proportions.TryGetValue((row, col), out var speciesMap))
            return 0;

        double total = speciesMap.Values.Sum();
        return total > 0 && speciesMap.TryGetValue(species, out var value)
            ? value / total
            : 0;
    }

    public double GetSpeciesBiomass(int row, int col, string species, double fgBiomass)
    {
        double pNorm = GetNormalizedProportion(row, col, species);
        return fgBiomass * pNorm;
    }

    /// <summary>
    /// Apply a local mortality rate to impact p. Just make sure that <paramref name="mort"/>
    /// and <paramref name="fgBiomass"/> are in the same units.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="col"></param>
    /// <param name="species">Species code</param>
    /// <param name="mort">Mortalty</param>
    /// <param name="fgBiomass">Total FG biomass</param>
    public void ApplyMortality(int row, int col, string species, double mort, double fgBiomass)
    {
        if (!m_proportions.TryGetValue((row, col), out var m_speciesMap)) return;
        if (!m_speciesMap.TryGetValue(species, out var p)) return;

        double deltaP = mort / fgBiomass;
        m_speciesMap[species] = Math.Max(0, p - deltaP);
    }

    /// <summary>
    /// 
    /// </summary>
    public void ApplyRecovery()
    {
        foreach (var (cell, speciesMap) in m_proportions)
        {
            foreach (var species in speciesMap.Keys.ToList())
            {
                if (!m_baselineProportions.TryGetValue(species, out var p0)) continue;

                var p = speciesMap[species];
                double drift = m_r * p * (1 - p / p0);
                speciesMap[species] += drift;
            }
        }

        #endregion // Public access
    }
}