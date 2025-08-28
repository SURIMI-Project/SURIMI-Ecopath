using Ecopath.EwE.Wrapper;
using EwECore;
using ControlledVocabularies.Core;

public static class GroupSpeciesProportionsFactory
{
    public static GroupSpeciesProportions Create(IEwECore core, int iGroup, IEnumerable<MultiLevelKey> mappings)
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
                IMultiLevelKeyField? code = key.GetField(SpeciesFields.SpeciesCode);
                if (code != null)
                    props.RegisterSpecies(key);
            }
        }

        return props;
    }
}
