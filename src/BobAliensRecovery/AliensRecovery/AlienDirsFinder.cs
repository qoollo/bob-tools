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

                    var alienDiskName = await bobApi.GetAlienDiskName();
                    if (alienDiskName != null)
                    {
                        if (await bobApi.RestartDisk(alienDiskName, cancellationToken))
                            _logger.LogDebug("Restarted alien disk {disk} on {node}", alienDiskName, node.Name);

                        var dir = await bobApi.GetAlienDirectory();
                        _logger.LogDebug("Found alien directory {dir} on {node}", dir.Path, node.Name);

                        result.Add(new AlienDir(node, dir));
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