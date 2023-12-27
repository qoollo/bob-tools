using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public class Remover
{
    private readonly IRemoteFileCopier _remoteFileCopier;
    private readonly ILogger<Remover> _logger;

    public Remover(IRemoteFileCopier remoteFileCopier, ILogger<Remover> logger)
    {
        _remoteFileCopier = remoteFileCopier;
        _logger = logger;
    }

    public async Task Remove(
        List<ConfirmedDeleteOperation> confirmed,
        List<RemoteDir> unconfirmed,
        bool forceRemoveUnconfirmed,
        CancellationToken cancellationToken
    )
    {
        if (await RemoveConfirmed(confirmed, cancellationToken) || forceRemoveUnconfirmed)
        {
            await RemoveUnconfirmed(unconfirmed, cancellationToken);
        }
    }

    private async Task<bool> RemoveConfirmed(
        List<ConfirmedDeleteOperation> operations,
        CancellationToken cancellationToken
    )
    {
        bool noErrors = true;
        foreach (var op in operations)
        {
            bool deleteAllowed = true;
            foreach (var copy in op.Copies)
            {
                if (
                    !await _remoteFileCopier.SourceCopiedToDest(
                        op.DirToDelete,
                        copy,
                        cancellationToken
                    )
                )
                {
                    noErrors = false;
                    _logger.LogError(
                        "Directories {From} and {To} contain different files, directory {From} can't be removed",
                        op.DirToDelete,
                        copy,
                        op.DirToDelete
                    );
                    deleteAllowed = false;
                    break;
                }
            }
            if (deleteAllowed)
            {
                if (await _remoteFileCopier.RemoveInDir(op.DirToDelete, cancellationToken))
                    _logger.LogInformation("Removed directory {From}", op.DirToDelete);
                else
                {
                    noErrors = false;
                    _logger.LogError("Failed to remove directory {From}", op.DirToDelete);
                }
            }
        }
        return noErrors;
    }

    private async Task RemoveUnconfirmed(List<RemoteDir> dirs, CancellationToken cancellationToken)
    {
        foreach (var dir in dirs)
        {
            if (await _remoteFileCopier.RemoveInDir(dir, cancellationToken))
                _logger.LogInformation("Removed directory {From}", dir);
            else
                _logger.LogError("Failed to remove directory {From}", dir);
        }
    }
}
