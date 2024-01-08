using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ClusterModifier;

public class ClusterStateAlterer
{
    private readonly ClusterExpandArguments _args;
    private readonly ICopier _copier;
    private readonly IRemover _remover;
    private readonly ILogger<ClusterStateAlterer> _logger;

    public ClusterStateAlterer(
        ClusterExpandArguments args,
        ICopier copier,
        IRemover remover,
        IValidator validator,
        ILogger<ClusterStateAlterer> logger
    )
    {
        _args = args;
        _copier = copier;
        _remover = remover;
        _logger = logger;
    }

    public async Task Alter(
        WorkSpecification workSpecification,
        CancellationToken cancellationToken
    )
    {
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
                _logger.LogInformation(
                    "Expected removing directory {Dir} without checking copies",
                    dir
                );
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
