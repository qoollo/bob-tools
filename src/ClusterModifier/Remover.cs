using System;
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
    private readonly ClusterExpandArguments _args;

    public Remover(
        IRemoteFileCopier remoteFileCopier,
        ILogger<Remover> logger,
        ClusterExpandArguments args
    )
    {
        _remoteFileCopier = remoteFileCopier;
        _logger = logger;
        _args = args;
    }

    public async Task<bool> RemoveConfirmed(
        List<ConfirmedDeleteOperation> operations,
        CancellationToken cancellationToken
    )
    {
        bool noErrors = true;
        foreach (var op in operations)
        {
            if (_args.DryRun)
                _logger.LogInformation("Expected removing files from {Directory}", op.DirToDelete);
            else
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
        }
        return noErrors;
    }

    public async Task RemoveUnconfirmed(List<RemoteDir> dirs, CancellationToken cancellationToken)
    {
        foreach (var dir in dirs)
        {
            if (_args.DryRun)
                _logger.LogInformation(
                    "Expected removing files from {Directory} (directory has no replicas)",
                    dir
                );
            else
            {
                if (await _remoteFileCopier.RemoveInDir(dir, cancellationToken))
                    _logger.LogInformation("Removed directory {From}", dir);
                else
                    _logger.LogError("Failed to remove directory {From}", dir);
            }
        }
    }
}
