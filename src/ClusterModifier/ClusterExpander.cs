using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.Exceptions;
using BobToolsCli.Helpers;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;

namespace ClusterModifier
{
    public class ClusterExpander
    {
        private readonly ILogger<ClusterExpander> _logger;
        private readonly IRemoteFileCopier _remoteFileCopier;
        private readonly ParallelP2PProcessor _parallelP2PProcessor;
        private readonly ClusterExpandArguments _args;

        public ClusterExpander(ILogger<ClusterExpander> logger,
                               IRemoteFileCopier remoteFileCopier,
                               ParallelP2PProcessor parallelP2PProcessor,
                               ClusterExpandArguments args)
        {
            _logger = logger;
            _remoteFileCopier = remoteFileCopier;
            _parallelP2PProcessor = parallelP2PProcessor;
            _args = args;
        }

        public async Task ExpandCluster(CancellationToken cancellationToken)
        {
            var oldConfigResult = await _args.GetClusterConfigurationFromFile(_args.OldConfigPath, cancellationToken);
            if (!oldConfigResult.IsOk(out var oldConfig, out var oldError))
                throw new ConfigurationException($"Old config is not available: {oldError}");
            var configResult = await _args.FindClusterConfiguration(cancellationToken: cancellationToken);
            if (!configResult.IsOk(out var config, out var newError))
                throw new ConfigurationException($"Current config is not available: {newError}");

            _logger.LogDebug("Expanding cluster from {OldConfigPath} to {CurrentConfigPath}",
                _args.OldConfigPath, _args.ClusterConfigPath);

            var dirsToDelete = await FindDirsToDelete(oldConfig, config, cancellationToken);
            var copyOperations = await CopyDataToNewReplicas(oldConfig, config, dirsToDelete, cancellationToken);

            if (_args.RemoveUnusedReplicas)
                await RemoveUnusedReplicas(oldConfig, config, copyOperations, dirsToDelete, cancellationToken);
        }

        private async Task<List<(RemoteDir from, RemoteDir to)>> CopyDataToNewReplicas(ClusterConfiguration oldConfig, ClusterConfiguration config,
            HashSet<RemoteDir> dirsToDelete, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Copying data from old to current replicas");
            var sourceDirsByDest = await GetSourceDirsByDestination(oldConfig, config, cancellationToken);
            var operations = CollectOperations(sourceDirsByDest, dirsToDelete);
            if (!_args.DryRun)
            {
                var parallelOperations = operations
                    .Select(op => ParallelP2PProcessor.CreateOperation(op.from.Address, op.to.Address,
                        () => Copy(op.from, op.to, cancellationToken)))
                    .ToArray();
                await _parallelP2PProcessor.Invoke(_args.CopyParallelDegree, parallelOperations, cancellationToken);
            }
            else
            {
                foreach(var (from, to) in operations)
                    _logger.LogInformation("Expected copying from {From} to {To}", from, to);
            }
            return operations;
        }

        private async Task OnVDiskDirs(ClusterConfiguration oldConfig, ClusterConfiguration config,
                Action<ClusterConfiguration.VDisk, IEnumerable<RemoteDir>, IEnumerable<RemoteDir>> onOldNewDirsForVdisk,
                CancellationToken cancellationToken)
        {
            var vDiskPairs = oldConfig.VDisks.Join(config.VDisks,
                                                   vd => vd.Id,
                                                   vd => vd.Id,
                                                   (ovd, vd) => (ovd, vd)); 
            var oldNodeInfoByName = await GetNodeInfoByName(oldConfig, cancellationToken);
            var nodeInfoByName = await GetNodeInfoByName(config, cancellationToken);
            foreach (var (oldVDisk, vDisk) in vDiskPairs)
            {
                var oldDirs = oldVDisk.Replicas.Select(r => oldNodeInfoByName[r.Node].GetRemoteDirForDisk(r.Disk, oldVDisk));
                var newDirs = vDisk.Replicas.Select(r => nodeInfoByName[r.Node].GetRemoteDirForDisk(r.Disk, vDisk));
                onOldNewDirsForVdisk(vDisk, oldDirs, newDirs);
            }
        }

        private async Task<Dictionary<RemoteDir, HashSet<RemoteDir>>> GetSourceDirsByDestination(ClusterConfiguration oldConfig,
            ClusterConfiguration config, CancellationToken cancellationToken)
        {
            var sourceDirsByDest = new Dictionary<RemoteDir, HashSet<RemoteDir>>();
            await OnVDiskDirs(oldConfig, config, (_, oldDirs, newDirs) =>
            {
                var missing = newDirs.Except(oldDirs);
                foreach (var newDir in missing)
                    if (sourceDirsByDest.TryGetValue(newDir, out var dirs))
                        dirs.UnionWith(oldDirs);
                    else
                        sourceDirsByDest.Add(newDir, oldDirs.ToHashSet());
            }, cancellationToken);
            return sourceDirsByDest;
        }

        private static List<(RemoteDir from, RemoteDir to)> CollectOperations(Dictionary<RemoteDir, HashSet<RemoteDir>> sourceDirsByDest,
            HashSet<RemoteDir> dirsToDelete)
        {
            var loadCountByAddress = new Dictionary<IPAddress, int>();
            var loadCountByDir = new Dictionary<RemoteDir, int>();
            foreach (var sources in sourceDirsByDest.Values)
                foreach (var src in sources)
                {
                    loadCountByAddress[src.Address] = 0;
                    loadCountByDir[src] = 0;
                }
            var operations = new List<(RemoteDir, RemoteDir)>();
            foreach (var (dest, sources) in sourceDirsByDest.OrderBy(kv => kv.Key.Address.ToString()).ThenBy(kv => kv.Key.Path))
            {
                var bestSource = sources
                    .OrderByDescending(rd => dirsToDelete.Contains(rd) && loadCountByDir[rd] == 0)
                    .ThenBy(rd => loadCountByAddress[rd.Address] - (rd.Address == dest.Address ? 1 : 0))
                    .ThenBy(rd => rd.Address.ToString()).ThenBy(rd => rd.Path).First();
                loadCountByAddress[bestSource.Address]++;
                loadCountByDir[bestSource]++;
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

        private async Task<HashSet<RemoteDir>> FindDirsToDelete(ClusterConfiguration oldConfig, ClusterConfiguration newConfig,
            CancellationToken cancellationToken)
        {
            var result = new HashSet<RemoteDir>();
            await OnVDiskDirs(oldConfig, newConfig, (vDisk, oldDirs, newDirs) =>
            {
                result.UnionWith(oldDirs.Except(newDirs));
            }, cancellationToken);
            return result;
        }

        private async Task RemoveUnusedReplicas(ClusterConfiguration oldConfig, ClusterConfiguration newConfig,
            IEnumerable<(RemoteDir from, RemoteDir to)> copyOperations, HashSet<RemoteDir> dirsToDelete,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Removing data from old replicas");
            var newDirsByOldDir = new Dictionary<RemoteDir, RemoteDir[]>();
            var oldDirsToDeleteWithoutCopy = new List<RemoteDir>();
            var copiedNewByOldDir = copyOperations
                .GroupBy(t => t.from)
                .ToDictionary(g => g.Key, g => g.Select(t => t.to).Distinct().ToArray());
            foreach(var oldDir in dirsToDelete)
            {
                if (copiedNewByOldDir.TryGetValue(oldDir, out var copiedNewDirs))
                {
                    newDirsByOldDir[oldDir] = copiedNewDirs;
                }
                else
                {
                    oldDirsToDeleteWithoutCopy.Add(oldDir);
                }
            }
            bool errorOccured = false;
            foreach (var (oldDirToDelete, newDirs) in newDirsByOldDir)
            {
                if (_args.DryRun)
                    _logger.LogInformation("Expected removing files from {Directory}", oldDirToDelete);
                else
                {
                    bool deleteAllowed = true;
                    foreach(var newDir in newDirs)
                    {
                        if (!await _remoteFileCopier.SourceCopiedToDest(oldDirToDelete, newDir, cancellationToken))
                        {
                            errorOccured = true;
                            _logger.LogError("Directories {From} and {To} contain different files, directory {From} can't be removed", 
                                    oldDirToDelete, newDir);
                            deleteAllowed = false;
                            break;
                        }
                    }
                    if (deleteAllowed)
                    {
                        if (await _remoteFileCopier.RemoveDirectory(oldDirToDelete, cancellationToken))
                            _logger.LogInformation("Removed directory {From}", oldDirToDelete);
                        else
                        {
                            errorOccured = true;
                            _logger.LogError("Failed to remove directory {From}", oldDirToDelete);
                        }
                    }
                }
            }
            if (errorOccured && !_args.ForceRemoveUncopiedUnusedReplicas)
                _logger.LogError("Error occured during removal of unused replicas with copies, will not remove replicas without copies");
            else
            {
                foreach(var oldDir in oldDirsToDeleteWithoutCopy)
                {
                    if (_args.DryRun)
                        _logger.LogInformation("Expected removing files from {Directory} (directory has no replicas)", oldDir);
                    else
                    {
                        if (await _remoteFileCopier.RemoveInDir(oldDir, cancellationToken))
                            _logger.LogInformation("Removed directory {From}", oldDir);
                        else
                            _logger.LogError("Failed to remove directory {From}", oldDir);
                    }
                }
            }
        }

        private async Task<bool> Copy(RemoteDir from, RemoteDir to, CancellationToken cancellationToken)
        {
            var copyResult = await _remoteFileCopier.Copy(from, to, cancellationToken);
            if (copyResult.IsError)
                throw new OperationException($"Failed to copy data from {from} to {to}");
            return true;
        }

        private async Task<HashSet<RemoteDir>> GetAllRemoteDirs(ClusterConfiguration config, CancellationToken cancellationToken)
        {
            var result = new HashSet<RemoteDir>();
            foreach (var node in config.Nodes)
            {
                var creator = await GetCreator(node, cancellationToken);
                foreach (var vDisk in config.VDisks)
                    foreach (var replica in vDisk.Replicas.Where(r => r.Node == node.Name))
                    {
                        var disk = node.Disks.Find(d => d.Name == replica.Disk);
                        if (disk == null)
                            throw new ClusterStateException($"Replica {replica} not found");

                        result.Add(creator(disk, vDisk));
                    }
            }
            return result;
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
                var client = _args.GetBobApiClientProvider().GetClient(node);
                var nodeConfigResult = await client.GetNodeConfiguration(cancellationToken);
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
