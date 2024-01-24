using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClusterModifier;

public class BobServersManipulator
{
    private readonly IBobDiskRestarter _bobDiskRestarter;

    public BobServersManipulator(IBobDiskRestarter bobDiskRestarter)
    {
        _bobDiskRestarter = bobDiskRestarter;
    }

    public async Task Manipulate(
        WorkSpecification workSpecification,
        CancellationToken cancellationToken
    )
    {
        var disksToRestart = CollectDisksToRestart(workSpecification);
        await RestartDisks(disksToRestart, cancellationToken);
    }

    private ImmutableArray<NodeDisk> CollectDisksToRestart(WorkSpecification workSpecification)
    {
        return workSpecification
            .CopyOperations.SelectMany(op => op.AffectedNewNodeDisks)
            .GroupBy(d => (d.Node.Name, d.DiskName))
            .Select(g => g.First())
            .Distinct()
            .ToImmutableArray();
    }

    private async Task RestartDisks(
        ImmutableArray<NodeDisk> disksToRestart,
        CancellationToken cancellationToken
    )
    {
        foreach (var disk in disksToRestart)
        {
            await _bobDiskRestarter.RestartDisk(disk, cancellationToken);
        }
    }
}
