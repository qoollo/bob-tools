using System;
using System.Collections.Generic;
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
        private readonly ClusterExpandArguments _args;

        public ClusterExpander(ILogger<ClusterExpander> logger, RemoteFileCopier remoteFileCopier,
            ClusterExpandArguments args)
        {
            _logger = logger;
            _remoteFileCopier = remoteFileCopier;
            _args = args;
        }

        public async Task ExpandCluster(CancellationToken cancellationToken)
        {
            var oldConfigResult = await _args.GetClusterConfigurationFromFile(_args.OldConfigPath, cancellationToken);
            if (!oldConfigResult.IsOk(out var oldConfig, out var oldError))
                throw new ConfigurationException($"Old config is not available: {oldError}");
            var configResult = await _args.FindClusterConfiguration(cancellationToken);
            if (!configResult.IsOk(out var config, out var newError))
                throw new ConfigurationException($"Current config is not available: {newError}");

            _logger.LogDebug("Expanding cluster from {OldConfigPath} to {CurrentConfigPath}",
                _args.OldConfigPath, _args.ClusterConfigPath);

            await CopyDataToNewReplicas(oldConfig, config, cancellationToken);

            if (_args.RemoveOldReplicas)
                await RemoveOldReplicas(oldConfig, config, cancellationToken);
        }

        private async Task CopyDataToNewReplicas(ClusterConfiguration oldConfig, ClusterConfiguration config,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Copying data from old to current replicas");

            var oldNodes = oldConfig.Nodes;
            var nodes = config.Nodes;
            var pairs = oldNodes.SelectMany(on => nodes.Select(n => (old: on, cur: n)))
                .Where(t => t.old.Name == t.cur.Name);
            var vDisksPairs = oldConfig.VDisks.SelectMany(ov => config.VDisks.Select(v => (old: ov, cur: v)))
                .Where(t => t.old.Id == t.cur.Id).ToArray();
            foreach (var (oldNode, node) in pairs)
            {
                var oldCreator = await GetCreator(oldNode, cancellationToken);
                var creator = await GetCreator(node, cancellationToken);
                foreach (var (oldVDisk, vDisk) in vDisksPairs)
                    foreach (var replica in vDisk.Replicas.Where(r => r.Node == node.Name))
                    {
                        var dir = creator(node.Disks.Find(d => d.Name == replica.Disk), vDisk);
                        var oldDirs = oldVDisk.Replicas.Select(or => oldCreator(oldNode.Disks.Find(d => d.Name == or.Disk), oldVDisk));
                        if (!await CopyDataFromAnyLocation(dir, oldDirs, cancellationToken))
                            throw new OperationException($"Failed to copy data for replica of {vDisk.Id} on {replica.Disk}");
                    }
            }
        }

        private async Task<bool> CopyDataFromAnyLocation(RemoteDir target, IEnumerable<RemoteDir> sources, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Trying to copy data to {Directory}", target);
            if (_args.DryRun || !sources.Any())
            {
                return true;
            }
            else
            {
                foreach (var source in sources)
                {
                    var copy = await _remoteFileCopier.CopyWithRsync(source, target, cancellationToken);
                    if (!copy.IsError)
                    {
                        _logger.LogInformation("Successfully copied data from {Source} to {Directory}", source, target);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to copy data from {Source} to {Directory}: {Error}", source, target,
                            string.Join(Environment.NewLine, copy.ErrorLines));
                    }
                }
                return false;
            }
        }

        private async Task RemoveOldReplicas(ClusterConfiguration oldConfig, ClusterConfiguration newConfig,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Removing data from old replicas");
            foreach (var vDisk in oldConfig.VDisks)
            {
                foreach (var replica in vDisk.Replicas.Where(r => !newConfig.VDisks.Any(vd => vd.Replicas.Any(r1 => r.Disk == r1.Disk && r.Node == r1.Node))))
                {
                    var node = oldConfig.Nodes.Find(n => n.Name == replica.Node);
                    var disk = node.Disks.Find(d => d.Name == replica.Disk);
                    var remoteDir = await GetRemoteDir(node, disk, vDisk, cancellationToken);
                    _logger.LogInformation("Removing {Directory}", remoteDir);
                    if (!_args.DryRun)
                    {
                        if (await _remoteFileCopier.RemoveInDir(remoteDir, cancellationToken))
                            _logger.LogDebug("Successfully removed {Directory}", remoteDir);
                        else
                            _logger.LogWarning("Failed to remove {Directory}", remoteDir);
                    }
                }
            }
        }

        private async ValueTask<RemoteDir> GetRemoteDir(ClusterConfiguration.Node node, ClusterConfiguration.Node.Disk disk,
            ClusterConfiguration.VDisk vDisk, CancellationToken cancellationToken = default)
        {
            var addr = await node.FindIPAddress();
            var apiAddr = _args.GetNodePortStorage().GetNodeApiUri(node);
            var nodeConfigResult = await new BobApi.BobApiClient(apiAddr).GetNodeConfiguration(cancellationToken);
            if (nodeConfigResult.IsOk(out var conf, out var error))
            {
                var dir = Path.Combine(disk.Path, conf.RootDir, vDisk.Id.ToString());
                return new RemoteDir(addr, dir);
            }
            else
                throw new ClusterStateException($"Node {node.Name} configuration is unavailable: {error}");
        }

        private async Task<Func<ClusterConfiguration.Node.Disk, ClusterConfiguration.VDisk, RemoteDir>> GetCreator(
            ClusterConfiguration.Node node, CancellationToken cancellationToken = default)
        {
            var addr = await node.FindIPAddress();
            var apiAddr = _args.GetNodePortStorage().GetNodeApiUri(node);
            var nodeConfigResult = await new BobApi.BobApiClient(apiAddr).GetNodeConfiguration(cancellationToken);
            if (nodeConfigResult.IsOk(out var conf, out var error))
            {
                return (disk, vdisk) => new RemoteDir(addr, Path.Combine(disk.Path, conf.RootDir, vdisk.Id.ToString()));
            }
            else
                throw new ClusterStateException($"Node {node.Name} configuration is unavailable: {error}");
        }
    }
}