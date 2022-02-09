using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobAliensRecovery.Exceptions;
using BobApi;
using BobApi.BobEntities;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;

namespace BobAliensRecovery.AliensRecovery
{
    public class ReplicasFinder
    {
        private readonly ILogger<ReplicasFinder> _logger;

        public ReplicasFinder(ILogger<ReplicasFinder> logger)
        {
            _logger = logger;
        }

        internal async Task<IReadOnlyDictionary<long, Replicas>> FindReplicasByVdiskId(ClusterConfiguration clusterConfiguration,
            ClusterOptions clusterOptions, AliensRecoveryOptions aliensRecoveryOptions,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<long, Replicas>();
            foreach (var vdisk in clusterConfiguration.VDisks)
            {
                var remoteDirByNodeName = await GetRemoteDirByNodeName(clusterConfiguration, clusterOptions,
                    aliensRecoveryOptions, vdisk, cancellationToken);
                result.Add(vdisk.Id, new Replicas(vdisk.Id, remoteDirByNodeName));
            }
            return result;
        }

        private async Task<Dictionary<string, RemoteDir>> GetRemoteDirByNodeName(
            ClusterConfiguration clusterConfiguration,
            ClusterOptions clusterOptions,
            AliensRecoveryOptions aliensRecoveryOptions,
            ClusterConfiguration.VDisk vdisk,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, RemoteDir>();
            var diskByName = vdisk.Replicas.ToDictionary(r => r.Node, r => r.Disk);
            foreach (var node in clusterConfiguration.Nodes)
            {
                if (diskByName.TryGetValue(node.Name, out var diskName))
                {
                    var disk = node.Disks.SingleOrDefault(d => d.Name == diskName);
                    if (disk != null)
                    {
                        using var bobApi = new BobApiClient(clusterOptions.GetNodeApiUri(node));
                        var nodeConfigurationResult = await bobApi.GetNodeConfiguration(cancellationToken);
                        if (nodeConfigurationResult.TryGetData(out var nodeConfiguration) && nodeConfiguration?.RootDir != null)
                        {
                            var targetPath = Path.Combine(disk.Path, nodeConfiguration.RootDir, vdisk.Id.ToString());
                            var addr = await node.FindIPAddress();
                            if (addr != null)
                                result.Add(node.Name, new RemoteDir(addr, targetPath));
                            else
                                aliensRecoveryOptions.LogErrorWithPossibleException<OperationException>(_logger,
                                "Failed to find ip address of {node}", node);
                        }
                        else
                        {
                            aliensRecoveryOptions.LogErrorWithPossibleException<ClusterStateException>(_logger,
                                "Failed to get node configuration from {node}", node);
                        }
                    }
                    else
                        throw new ConfigurationException("Configuration does not match running cluster");
                }
            }

            return result;
        }
    }
}