using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobToolsCli.Exceptions;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public class Validator : IValidator
{
    private readonly ClusterExpandArguments _args;
    private readonly IRemoteFileCopier _remoteFileCopier;
    private readonly ILogger<Validator> _logger;

    public Validator(
        ClusterExpandArguments args,
        IRemoteFileCopier remoteFileCopier,
        ILogger<Validator> logger
    )
    {
        _args = args;
        _remoteFileCopier = remoteFileCopier;
        _logger = logger;
    }

    public async Task Validate(ClusterState clusterState, CancellationToken cancellationToken)
    {
        if (!_args.SkipAlienPresenceCheck)
        {
            var fileContainingAlienDirs = await FindFileContainingDirs(
                clusterState.AlienDirs,
                cancellationToken
            );
            if (fileContainingAlienDirs.Count > 0)
            {
                foreach (var dir in fileContainingAlienDirs)
                {
                    _logger.LogError("Alien dir contain files: {Dir}", dir);
                }
                throw new ClusterStateException(
                    $"{fileContainingAlienDirs.Count} alien dirs contain files"
                );
            }
        }
    }

    private async Task<List<RemoteDir>> FindFileContainingDirs(
        List<RemoteDir> alienDirs,
        CancellationToken cancellationToken
    )
    {
        var result = new List<RemoteDir>();
        foreach (var dir in alienDirs)
        {
            if (
                await _remoteFileCopier.DirContainsFiles(
                    dir,
                    recursive: true,
                    cancellationToken: cancellationToken
                )
            )
                result.Add(dir);
        }
        return result;
    }
}
