using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ClusterModifier;

public class ClusterExpander
{
    private readonly ClusterStateFinder _clusterStateFinder;
    private readonly WorkSpecificationFinder _workSpecificationFinder;
    private readonly Copier _copier;
    private readonly Remover _remover;
    private readonly ClusterExpandArguments _args;
    private readonly ILogger<ClusterExpander> _logger;

    public ClusterExpander(
        ClusterStateFinder clusterStateFinder,
        WorkSpecificationFinder workSpecificationFinder,
        Copier copier,
        Remover remover,
        ClusterExpandArguments args,
        ILogger<ClusterExpander> logger
    )
    {
        _clusterStateFinder = clusterStateFinder;
        _workSpecificationFinder = workSpecificationFinder;
        _copier = copier;
        _remover = remover;
        _args = args;
        _logger = logger;
    }

    public async Task ExpandCluster(CancellationToken cancellationToken)
    {
        var clusterState = await _clusterStateFinder.Find(cancellationToken);
        var workSpecification = _workSpecificationFinder.Find(clusterState);

        await Copy(workSpecification, cancellationToken);

        if (_args.RemoveUnusedReplicas)
        {
            await Remove(workSpecification, cancellationToken);
        }
    }

    private async Task Copy(
        WorkSpecification workSpecification,
        CancellationToken cancellationToken
    )
    {
        if (_args.DryRun)
        {
            foreach (var op in workSpecification.CopyOperations)
                _logger.LogInformation("Expected copying from {From} to {To}", op.From, op.To);
        }
        else
        {
            _logger.LogInformation("Copying data from old to current replicas");
            await _copier.Copy(
                workSpecification.CopyOperations,
                _args.CopyParallelDegree,
                cancellationToken
            );
        }
    }

    private async Task Remove(
        WorkSpecification workSpecification,
        CancellationToken cancellationToken
    )
    {
        if (_args.DryRun)
        {
            foreach (var op in workSpecification.ConfirmedDeleteOperations)
                _logger.LogInformation(
                    "Expected removing directory {Dir} with checking copies",
                    op.DirToDelete
                );
            foreach (var dir in workSpecification.UnconfirmedDeleteDirs)
                _logger.LogInformation("Expected removing directory {Dir} without checking copies");
        }
        else
        {
            _logger.LogInformation("Removing data from obsolete replicas");
            await _remover.Remove(
                workSpecification.ConfirmedDeleteOperations,
                workSpecification.UnconfirmedDeleteDirs,
                _args.ForceRemoveUncopiedUnusedReplicas,
                cancellationToken
            );
        }
    }
}
