using System.Threading;
using System.Threading.Tasks;

namespace ClusterModifier;

public class ClusterExpander
{
    private readonly ClusterStateFinder _clusterStateFinder;
    private readonly WorkSpecificationFinder _workSpecificationFinder;
    private readonly Copier _copier;
    private readonly Remover _remover;
    private readonly ClusterExpandArguments _args;

    public ClusterExpander(
        ClusterStateFinder clusterStateFinder,
        WorkSpecificationFinder workSpecificationFinder,
        Copier copier,
        Remover remover,
        ClusterExpandArguments args
    )
    {
        _clusterStateFinder = clusterStateFinder;
        _workSpecificationFinder = workSpecificationFinder;
        _copier = copier;
        _remover = remover;
        _args = args;
    }

    public async Task ExpandCluster(CancellationToken cancellationToken)
    {
        var clusterState = await _clusterStateFinder.Find(cancellationToken);
        var workSpecification = _workSpecificationFinder.Find(clusterState);

        await _copier.Copy(workSpecification.CopyOperations, cancellationToken);

        if (_args.RemoveUnusedReplicas)
        {
            if (
                await _remover.RemoveConfirmed(
                    workSpecification.ConfirmedDeleteOperations,
                    cancellationToken
                ) || _args.ForceRemoveUncopiedUnusedReplicas
            )
            {
                await _remover.RemoveUnconfirmed(
                    workSpecification.UnconfirmedDeleteDirs,
                    cancellationToken
                );
            }
        }
    }
}
