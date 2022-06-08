using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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

            if (_args.RemoveUnusedReplicas)
                await RemoveUnusedReplicas(oldConfig, config, cancellationToken);
        }

        private async Task CopyDataToNewReplicas(ClusterConfiguration oldConfig, ClusterConfiguration config,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Copying data from old to current replicas");
            var sourceDirsByDest = await GetSourceDirsByDestination(oldConfig, config, cancellationToken);
            var operations = CollectOperations(sourceDirsByDest);
            if (!_args.DryRun)
                foreach (var (src, dst) in operations)
                    await _remoteFileCopier.CopyWithRsync(src, dst, cancellationToken);
        }

        private async Task<Dictionary<RemoteDir, HashSet<RemoteDir>>> GetSourceDirsByDestination(ClusterConfiguration oldConfig,
            ClusterConfiguration config, CancellationToken cancellationToken)
        {
            var vDiskPairs = oldConfig.VDisks.SelectMany(ovd => config.VDisks.Select(vd => (ovd, vd)))
                .Where(t => t.ovd.Id == t.vd.Id);
            var oldNodeInfoByName = await GetNodeInfoByName(oldConfig, cancellationToken);
            var nodeInfoByName = await GetNodeInfoByName(config, cancellationToken);
            var sourceDirsByDest = new Dictionary<RemoteDir, HashSet<RemoteDir>>();
            foreach (var (oldVDisk, vDisk) in vDiskPairs)
            {
                var oldDirs = oldVDisk.Replicas.Select(r => oldNodeInfoByName[r.Node].GetRemoteDirForDisk(r.Disk, oldVDisk));
                var newDirs = vDisk.Replicas.Select(r => nodeInfoByName[r.Node].GetRemoteDirForDisk(r.Disk, vDisk));
                var missing = newDirs.Except(oldDirs);
                foreach (var newDir in missing)
                    if (sourceDirsByDest.TryGetValue(newDir, out var dirs))
                        dirs.UnionWith(oldDirs);
                    else
                        sourceDirsByDest.Add(newDir, oldDirs.ToHashSet());
            }

            return sourceDirsByDest;
        }

        private static List<(RemoteDir, RemoteDir)> CollectOperations(Dictionary<RemoteDir, HashSet<RemoteDir>> sourceDirsByDest)
        {
            var loadCount = new Dictionary<IPAddress, int>();
            foreach (var sources in sourceDirsByDest.Values)
                foreach (var src in sources)
                    loadCount[src.Address] = 0;
            var operations = new List<(RemoteDir, RemoteDir)>();
            foreach (var (dest, sources) in sourceDirsByDest.OrderBy(kv => kv.Key.Address).ThenBy(kv => kv.Key.Path))
            {
                var bestSource = sources
                    .OrderBy(rd => loadCount[rd.Address])
                    .ThenBy(rd => rd.Address).ThenBy(rd => rd.Path).First();
                loadCount[bestSource.Address]++;
                operations.Add((bestSource, dest));
            }

            return operations;
        }

        private async Task<Dictionary<string, NodeInfo>> GetNodeInfoByName(ClusterConfiguration config, CancellationToken cancellationToken)
        {
            var nodeInfoByName = new Dictionary<string, NodeInfo>();
            foreach (var node in config.Nodes)
            {
                var cr = await GetCreator(node, cancellationToken);
                var disks = node.Disks.ToDictionary(d => d.Name);
                nodeInfoByName.Add(node.Name, new NodeInfo(node, cr, disks));
            }

            return nodeInfoByName;
        }

        private async Task RemoveUnusedReplicas(ClusterConfiguration oldConfig, ClusterConfiguration newConfig,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Removing data from old replicas");
            var newRemoteDirs = await GetAllRemoteDirs(newConfig, cancellationToken);
            var oldRemoteDirs = await GetAllRemoteDirs(oldConfig, cancellationToken);
            foreach (var remoteDir in oldRemoteDirs.Except(newRemoteDirs))
                await RemoveDir(remoteDir, cancellationToken);
        }

        private async Task<HashSet<RemoteDir>> GetAllRemoteDirs(ClusterConfiguration config, CancellationToken cancellationToken)
        {
            var result = new HashSet<RemoteDir>();
            foreach (var node in config.Nodes)
            {
                var creator = await GetCreator(node, cancellationToken);
                foreach (var vDisk in config.VDisks)
                    foreach (var replica in vDisk.Replicas.Where(r => r.Node == node.Name))
                        result.Add(creator(node.Disks.Find(d => d.Name == replica.Disk), vDisk));
            }
            return result;
        }

        private async Task RemoveDir(RemoteDir remoteDir, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Removing {Directory}", remoteDir);
            if (!_args.DryRun)
            {
                if (await _remoteFileCopier.RemoveInDir(remoteDir, cancellationToken))
                    _logger.LogDebug("Successfully removed {Directory}", remoteDir);
                else
                    _logger.LogWarning("Failed to remove {Directory}", remoteDir);
            }
        }

        private delegate RemoteDir GetRemoteDir(ClusterConfiguration.Node.Disk disk, ClusterConfiguration.VDisk vDisk);
        private async ValueTask<GetRemoteDir> GetCreator(
            ClusterConfiguration.Node node, CancellationToken cancellationToken = default)
        {
            var addr = await node.FindIPAddress();
            var rootDir = await GetRootDir(node, cancellationToken);
            return (disk, vdisk) => new RemoteDir(addr, Path.Combine(disk.Path, rootDir, vdisk.Id.ToString()));
        }

        private async ValueTask<string> GetRootDir(ClusterConfiguration.Node node, CancellationToken cancellationToken = default)
        {
            var rootDir = _args.FindRootDir(node.Name);
            if (rootDir == null)
            {
                var apiAddr = _args.GetNodePortStorage().GetNodeApiUri(node);
                var nodeConfigResult = await new BobApi.BobApiClient(apiAddr).GetNodeConfiguration(cancellationToken);
                if (nodeConfigResult.IsOk(out var conf, out var error))
                    rootDir = conf.RootDir;
                else
                    throw new ClusterStateException($"Node {node.Name} configuration is unavailable: {error}, " +
                        "and bob-root-dir does not contain enough information");
            }
            return rootDir;
        }

        private readonly struct NodeInfo
        {
            private readonly GetRemoteDir _getRemoteDir;
            private readonly Dictionary<string, ClusterConfiguration.Node.Disk> _diskByName;
            private readonly ClusterConfiguration.Node _node;

            public NodeInfo(ClusterConfiguration.Node node,
                GetRemoteDir getRemoteDir, Dictionary<string, ClusterConfiguration.Node.Disk> diskByName)
            {
                _node = node;
                _getRemoteDir = getRemoteDir;
                _diskByName = diskByName;
            }

            public RemoteDir GetRemoteDirForDisk(string diskName, ClusterConfiguration.VDisk vDisk)
                => _diskByName.TryGetValue(diskName, out var disk)
                    ? _getRemoteDir(disk, vDisk)
                    : throw new ClusterStateException($"Disk {diskName} not found on node {_node.Name}");
        }
    }
}