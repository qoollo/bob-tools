using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobToolsCli.Exceptions;
using BobToolsCli.Helpers;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;

namespace ClusterModifier;

public class Copier
{
    private readonly IRemoteFileCopier _remoteFileCopier;
    private readonly ParallelP2PProcessor _parallelP2PProcessor;
    private readonly ILogger<Copier> _logger;
    private readonly ClusterExpandArguments _args;

    public Copier(
        IRemoteFileCopier remoteFileCopier,
        ParallelP2PProcessor parallelP2PProcessor,
        ILogger<Copier> logger,
        ClusterExpandArguments args
    )
    {
        _remoteFileCopier = remoteFileCopier;
        _parallelP2PProcessor = parallelP2PProcessor;
        _logger = logger;
        _args = args;
    }

    public async Task Copy(List<CopyOperation> copyOperations, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Copying data from old to current replicas");
        if (!_args.DryRun)
        {
            var parallelOperations = copyOperations
                .Select(
                    op =>
                        ParallelP2PProcessor.CreateOperation(
                            op.From.Address,
                            op.To.Address,
                            () => Copy(op, cancellationToken)
                        )
                )
                .ToArray();
            await _parallelP2PProcessor.Invoke(
                _args.CopyParallelDegree,
                parallelOperations,
                cancellationToken
            );
        }
        else
        {
            foreach (var op in copyOperations)
                _logger.LogInformation("Expected copying from {From} to {To}", op.From, op.To);
        }
    }

    private async Task<bool> Copy(CopyOperation op, CancellationToken cancellationToken)
    {
        var copyResult = await _remoteFileCopier.Copy(op.From, op.To, cancellationToken);
        if (copyResult.IsError)
            throw new OperationException($"Failed to copy data from {op.From} to {op.To}");
        return true;
    }
}
