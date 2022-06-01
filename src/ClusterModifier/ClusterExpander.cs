using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.Exceptions;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;

namespace ClusterModifier
{
    public class ClusterExpander
    {
        private readonly ILogger<ClusterExpander> _logger;
        private readonly RemoteFileCopier _remoteFileCopier;

        public ClusterExpander(ILogger<ClusterExpander> logger, RemoteFileCopier remoteFileCopier)
        {
            _logger = logger;
            _remoteFileCopier = remoteFileCopier;
        }

        public async Task ExpandCluster(ClusterExpandArguments args, CancellationToken cancellationToken)
        {
            var oldConfigResult = await args.GetClusterConfigurationFromFile(args.OldConfigPath, cancellationToken);
            if (!oldConfigResult.IsOk(out var oldConfig, out var oldError))
                throw new ConfigurationException($"Old config is not available: {oldError}");
            var newConfigResult = await args.FindClusterConfiguration(cancellationToken);
            if (!newConfigResult.IsOk(out var newConfig, out var newError))
                throw new ConfigurationException($"New config is not available: {newError}");

            _logger.LogDebug("Expanding cluster from {OldConfigPath} to {NewConfigPath}",
                args.OldConfigPath, args.ClusterConfigPath);

            foreach (var vdisk in newConfig.VDisks)
            {
                using var vDiskScope = _logger.BeginScope("VDisk {vdiskId}", vdisk.Id);
                _logger.LogDebug("Analyzing vdisk from new config");
                var oldVdisk = oldConfig.VDisks.Find(vd => vd.Id == vdisk.Id);
                if (oldVdisk != null && oldVdisk.Replicas.Count > 0)
                {
                    foreach (var replica in vdisk.Replicas)
                    {
                        using var replicaScope = _logger.BeginScope("Replica = {replicaNode}-{replicaDisk}", replica.Node, replica.Disk);
                        _logger.LogDebug("Analyzing replica from new config");
                        var node = newConfig.Nodes.Find(n => n.Name == replica.Node);
                        var disk = node.Disks.Find(d => d.Name == replica.Disk);
                        using var pathScope = _logger.BeginScope("Path = {replicaPath}", disk.Path);
                        var oldReplica = oldVdisk.Replicas.Find(r => r.Node == replica.Node && r.Disk == replica.Disk);
                        if (oldReplica != null)
                            _logger.LogDebug("Found replica in old config");
                        else
                        {
                            _logger.LogWarning("Replica not found in old config, restoring data...");
                            foreach (var selectedReplica in oldVdisk.Replicas)
                            {
                                var oldNode = oldConfig.Nodes.Find(n => n.Name == selectedReplica.Node);
                                var oldDisk = oldNode.Disks.Find(d => d.Name == selectedReplica.Disk);

                                var oldAddr = await oldNode.FindIPAddress();
                                var nodeAddr = await node.FindIPAddress();

                                var oldDir = Path.Combine(oldDisk.Path, "bob", vdisk.Id.ToString()); // TODO Use dir from bob config
                                var newDir = Path.Combine(disk.Path, "bob", vdisk.Id.ToString()); // TODO Use dir from bob config

                                var oldRemoteDir = new RemoteDir(oldAddr, oldDir);
                                var remoteDir = new RemoteDir(nodeAddr, newDir);
                                _logger.LogInformation("Trying to copy data from {Old} to {Current}", oldRemoteDir, remoteDir);
                                if (args.DryRun)
                                {
                                    break;
                                }
                                else
                                {
                                    var copy = await _remoteFileCopier.CopyWithRsync(oldRemoteDir, remoteDir, cancellationToken);
                                    if (!copy.IsError)
                                    {
                                        _logger.LogInformation("Successfully copied data from {Old} to {Current}", oldRemoteDir, remoteDir);
                                        break;
                                    }
                                    else
                                        _logger.LogWarning("Failed to copy data from {Old} to {Current}: {Error}", oldRemoteDir, remoteDir,
                                            string.Join(Environment.NewLine, copy.ErrorLines));
                                }
                            }
                        }
                    }
                }
                else
                    _logger.LogDebug("Vdisk's replicas not found in old config");
            }

            if (args.RemoveSourceFiles)
                foreach (var vDisk in oldConfig.VDisks)
                {
                    using var vDiskScope = _logger.BeginScope("VDisk {vdiskId}", vDisk.Id);
                    foreach (var replica in vDisk.Replicas.Where(r => !newConfig.VDisks.Any(vd => vd.Replicas.Any(r1 => r.Disk == r1.Disk && r.Node == r1.Node))))
                    {
                        using var replicaScope = _logger.BeginScope("Replica = {replicaNode}-{replicaDisk}", replica.Node, replica.Disk);
                        var node = oldConfig.Nodes.Find(n => n.Name == replica.Node);
                        var disk = node.Disks.Find(d => d.Name == replica.Disk);
                        var addr = await node.FindIPAddress();
                        var dir = Path.Combine(disk.Path, "bob", vDisk.Id.ToString()); // TODO Use dir from bob config
                        var remoteDir = new RemoteDir(addr, dir);
                        _logger.LogInformation("Removing data from {Directory}", remoteDir);
                        if (!args.DryRun)
                        {
                            if (await _remoteFileCopier.RemoveInDir(remoteDir, cancellationToken))
                                _logger.LogDebug("Successfully removed content of {Directory}", remoteDir);
                            else
                                _logger.LogWarning("Failed to remove content of {Directory}", remoteDir);
                        }
                    }
                }
        }

        private async ValueTask<RemoteDir> GetRemoteDir(ClusterConfiguration.Node node, ClusterConfiguration.Node.Disk disk,
            ClusterConfiguration.VDisk vDisk, ClusterExpandArguments args, CancellationToken cancellationToken = default)
        {
            var addr = await node.FindIPAddress();
            var apiAddr = args.GetNodePortStorage().GetNodeApiUri(node);
            var nodeConfigResult = await new BobApi.BobApiClient(apiAddr).GetNodeConfiguration(cancellationToken);
            if (nodeConfigResult.IsOk(out var conf, out var error))
            {
                return null;
            }
            else
                throw new ClusterStateException($"Node {node.Name} configuration is unavailable: {error}");
        }
    }
}