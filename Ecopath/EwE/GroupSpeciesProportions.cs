using Ecopath.EwE;

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
    #region Private vars 

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
    /// Cells in need of normalization.
    /// </summary>
    private readonly HashSet<(int row, int col)> m_dirtyCells = new();

    /// <summary>
    /// Species baseline group proportion (p0)
    /// </summary>
    private readonly Dictionary<string, double> m_baselineProportions = new();

    #endregion // Private vars 

    /// -----------------------------------------------------------------------
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="iGroup"></param>
    /// <param name="r"></param>
    /// <param name="activeCells"></param>
    /// -----------------------------------------------------------------------
    public GroupSpeciesProportions(int iGroup, double r, IEnumerable<(int row, int col)> activeCells)
    {
        this.m_iGroup = iGroup;
        this.m_r = r;

        foreach (var cell in activeCells)
            m_proportions[cell] = new Dictionary<string, double>();
    }

    #region Public access

    /// -----------------------------------------------------------------------
    /// <summary>
    /// Add a species to the group administration
    /// </summary>
    /// <param name="species"></param>
    /// -----------------------------------------------------------------------
    public void RegisterSpecies(EwEMapping species)
    {
        string key = Key(species);
        m_baselineProportions[key] = species.Proportion;
        foreach (var cell in m_proportions.Values)
            cell[key] = species.Proportion;
    }

    /// -----------------------------------------------------------------------
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    /// -----------------------------------------------------------------------
    public void NormalizeDirtyCells()
    {
        foreach (var cell in m_dirtyCells)
        {
            var props = m_proportions[cell];
            double total = props.Values.Sum();

            if (total > 0)
                foreach (var code in props.Keys.ToList())
                    props[code] = Math.Max(0.0001, Math.Round(props[code] / total, 2));
        }
        m_dirtyCells.Clear();
    }

    public double GetSpeciesBiomass(int row, int col, EwEMapping species, double fgBiomass)
    {
        var cell = (row, col);
        string key = Key(species);
        return m_proportions.TryGetValue(cell, out var props) && props.TryGetValue(key, out var p)
          ? fgBiomass * p
          : 0;
    }

    /// -----------------------------------------------------------------------
    /// <summary>
    /// Apply a local mortality rate to impact p. Just make sure that <paramref name="mort"/>
    /// and <paramref name="fgBiomass"/> are in the same units.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="col"></param>
    /// <param name="species">Species code</param>
    /// <param name="mort">Mortalty</param>
    /// <param name="fgBiomass">Total FG biomass</param>
    /// -----------------------------------------------------------------------
    public void ApplyFishingMortality(int row, int col, EwEMapping species, double mort, double fgBiomass)
    {
        string key = Key(species);
        var cell = (row, col);

        if (!m_proportions.TryGetValue(cell, out var m_speciesMap)) return;
        if (!m_speciesMap.TryGetValue(key, out var p)) return;

        double deltaP = mort / fgBiomass;
        m_speciesMap[key] = Math.Max(0, p - deltaP);

        m_dirtyCells.Add(cell);
    }

    /// -----------------------------------------------------------------------
    /// <summary>
    /// 
    /// </summary>
    /// -----------------------------------------------------------------------
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
    }

    #endregion // Public access

    #region Internals 

    private string Key(MultiLevelKey key)
    {
        return key.ToString();
    }

    #endregion // Internals
}