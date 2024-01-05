using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobToolsCli.Exceptions;
using BobToolsCli.Helpers;
using RemoteFileCopy;

namespace ClusterModifier;

public class Copier : ICopier
{
    private readonly IRemoteFileCopier _remoteFileCopier;
    private readonly ParallelP2PProcessor _parallelP2PProcessor;

    public Copier(IRemoteFileCopier remoteFileCopier, ParallelP2PProcessor parallelP2PProcessor)
    {
        _remoteFileCopier = remoteFileCopier;
        _parallelP2PProcessor = parallelP2PProcessor;
    }

    public async Task Copy(
        List<CopyOperation> copyOperations,
        int copyParallelDegree,
        CancellationToken cancellationToken
    )
    {
        var parallelOperations = copyOperations
            .Select(
                op =>
                    ParallelP2PProcessor.CreateOperation(
                        op.From.Address,
                        op.To.Address,
                        () => InvokeOperation(op, cancellationToken)
                    )
            )
            .ToArray();
        await _parallelP2PProcessor.Invoke(
            copyParallelDegree,
            parallelOperations,
            cancellationToken
        );
    }

    private async Task<bool> InvokeOperation(CopyOperation op, CancellationToken cancellationToken)
    {
        var copyResult = await _remoteFileCopier.Copy(op.From, op.To, cancellationToken);
        if (copyResult.IsError)
            throw new OperationException($"Failed to copy data from {op.From} to {op.To}");
        return true;
    }
}
