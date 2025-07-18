using Ecopath.EwE;
using EwECore;

public static class GroupSpeciesProportionsFactory
{
    public static GroupSpeciesProportions Create(cCore core, int iGroup, IEnumerable<MultiLevelKey> mappings)
    {
        cEcopathDataStructures pathDS = core.EcopathDataStructures;
        cEcospaceDataStructures spaceDS = core.EcospaceDataStructures;

        double PB = pathDS.PB[iGroup];
        double stepsPerYear = 1 / spaceDS.TimeStep;

        double r = 1.0 - Math.Exp(- PB / stepsPerYear);

        List<(int row, int col)> activeCells = new();
        for (int row = 1; row <= spaceDS.InRow; row++)
            for (int col = 1; col <= spaceDS.InCol; col++)
                if (spaceDS.Depth[row, col] > 0)
                    activeCells.Add((row, col));

        var props = new GroupSpeciesProportions(iGroup, r, activeCells);

        foreach (EwEMapping key in mappings)
        {
            if (key.Index == iGroup)
            {
                string code = key.GetField(SpeciesFields.SpeciesCode);
                props.RegisterSpecies(key);
            }
        }

        return props;
    }
}
