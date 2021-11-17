using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobAliensRecovery.Exceptions;
using BobApi;
using BobApi.BobEntities;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery.AliensRecovery
{
    public class AlienDirsFinder
    {
        private readonly ILogger<AlienDirsFinder> _logger;

        public AlienDirsFinder(ILogger<AlienDirsFinder> logger)
        {
            _logger = logger;
        }

        internal async Task<IEnumerable<AlienDir>> FindAlienDirs(ClusterConfiguration clusterConfiguration,
            ClusterOptions clusterOptions, AliensRecoveryOptions aliensRecoveryOptions,
            CancellationToken cancellationToken)
        {
            var result = new HashSet<AlienDir>();
            foreach (var node in clusterConfiguration.Nodes)
            {
                using var bobApi = new BobApiClient(clusterOptions.GetNodeApiUri(node));

                var syncResult = await bobApi.SyncAlienData(cancellationToken);
                if (!syncResult.TryGetData(out var isSynced) || !isSynced)
                    aliensRecoveryOptions.LogErrorWithPossibleException<ClusterStateException>(
                        _logger, "Failed to sync alien dir on node {node}", node);

                var dirResult = await bobApi.GetAlienDirectory(cancellationToken);
                if (dirResult.TryGetData(out var dir) && dir?.Path != null)
                {
                    _logger.LogDebug("Found alien directory {dir} on {node}", dir.Path, node.Name);

                    result.Add(new AlienDir(node, dir));
                }
                else
                    aliensRecoveryOptions.LogErrorWithPossibleException<ClusterStateException>(
                        _logger, "Failed to get alien dir from {node}", node);
            }

            return result;
        }
    }
}