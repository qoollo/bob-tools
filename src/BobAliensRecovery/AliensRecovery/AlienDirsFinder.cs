using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
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
            ClusterOptions clusterOptions, CancellationToken cancellationToken)
        {
            var result = new HashSet<AlienDir>();
            foreach (var node in clusterConfiguration.Nodes)
            {
                try
                {
                    using var bobApi = new BobApiClient(clusterOptions.GetNodeApiUri(node));

                    {
                        if (!await bobApi.SyncAlienData(cancellationToken))
                            _logger.LogDebug("Failed to sync alien data on node {node}", node.Name);

                        var dir = await bobApi.GetAlienDirectory();
                        if (dir.Path is null)
                            _logger.LogWarning("Failed to get alien directory from node {node}", node.Name);
                        else
                        {
                            _logger.LogDebug("Found alien directory {dir} on {node}", dir.Path, node.Name);

                            result.Add(new AlienDir(node, dir));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to get aliens info from {node}", node.Name);
                }
            }

            return result;
        }
    }
}