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
            var vDiskInfo = await GetVDiskInfo(cancellationToken);
            var dirsToDelete = GetDirsToDelete(vDiskInfo);
            var copyOperations = GetCopyOperations(vDiskInfo, dirsToDelete);

            await CopyDataToNewReplicas(copyOperations, cancellationToken);

            if (_args.RemoveUnusedReplicas)
            {
                var confirmedDeleteOperations = GetConfirmedDeleteOperations(copyOperations, dirsToDelete);
                if (await InvokeConfirmedDeleteOperations(confirmedDeleteOperations, cancellationToken)
                    || _args.ForceRemoveUncopiedUnusedReplicas)
                {
                    var unconfirmedDirs = GetUnconfirmedDeleteDirs(copyOperations, dirsToDelete);
                    await InvokeUnconfirmedDeleteOperations(unconfirmedDirs, cancellationToken);
                }
            }
        }

        private async Task<List<VDiskInfo>> GetVDiskInfo(CancellationToken cancellationToken)
        {
            var oldConfigResult = await _args.GetClusterConfigurationFromFile(_args.OldConfigPath, cancellationToken);
            if (!oldConfigResult.IsOk(out var oldConfig, out var oldError))
                throw new ConfigurationException($"Old config is not available: {oldError}");

            var configResult = await _args.FindClusterConfiguration(cancellationToken: cancellationToken);
            if (!configResult.IsOk(out var config, out var newError))
                throw new ConfigurationException($"Current config is not available: {newError}");

            _logger.LogDebug("Expanding cluster from {OldConfigPath} to {CurrentConfigPath}",
                _args.OldConfigPath, _args.ClusterConfigPath);

            return await GetVDiskInfo(oldConfig, config, cancellationToken);
        }

        private async Task CopyDataToNewReplicas(List<CopyOperation> operations, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Copying data from old to current replicas");
            if (!_args.DryRun)
            {
                var parallelOperations = operations
                    .Select(op => ParallelP2PProcessor.CreateOperation(op.From.Address, op.To.Address,
                        () => Copy(op, cancellationToken)))
                    .ToArray();
                await _parallelP2PProcessor.Invoke(_args.CopyParallelDegree, parallelOperations, cancellationToken);
            }
            else
            {
                foreach(var (from, to) in operations)
                    _logger.LogInformation("Expected copying from {From} to {To}", from, to);
            }
        }

        private List<CopyOperation> GetCopyOperations(List<VDiskInfo> vDiskInfo, HashSet<RemoteDir> dirsToDelete)
        {
            var sourceDirsByDest = GetSourceDirsByDestination(vDiskInfo);
            return CollectOperations(sourceDirsByDest, dirsToDelete);
        }

        private async Task<List<VDiskInfo>> GetVDiskInfo(ClusterConfiguration oldConfig, ClusterConfiguration config,
                CancellationToken cancellationToken)
        {
            var vDiskPairs = oldConfig.VDisks.Join(config.VDisks,
                                                   vd => vd.Id,
                                                   vd => vd.Id,
                                                   (ovd, vd) => (ovd, vd)); 
            var oldNodeInfoByName = await GetNodeInfoByName(oldConfig, cancellationToken);
            var nodeInfoByName = await GetNodeInfoByName(config, cancellationToken);
            var result = new List<VDiskInfo>();
            foreach (var (oldVDisk, vDisk) in vDiskPairs)
            {
                var oldDirs = oldVDisk.Replicas.Select(r => oldNodeInfoByName[r.Node].GetRemoteDirForDisk(r.Disk, oldVDisk));
                var newDirs = vDisk.Replicas.Select(r => nodeInfoByName[r.Node].GetRemoteDirForDisk(r.Disk, vDisk));
                result.Add(new VDiskInfo(vDisk, oldDirs.ToArray(), newDirs.ToArray()));
            }
            return result;
        }

        public record struct VDiskInfo(ClusterConfiguration.VDisk VDisk, RemoteDir[] OldDirs, RemoteDir[] NewDirs);

        private Dictionary<RemoteDir, HashSet<RemoteDir>> GetSourceDirsByDestination(List<VDiskInfo> vDiskInfo)
        {
            var sourceDirsByDest = new Dictionary<RemoteDir, HashSet<RemoteDir>>();
            foreach(var info in vDiskInfo)
            {
                var missing = info.NewDirs.Except(info.OldDirs);
                foreach (var newDir in missing)
                    if (sourceDirsByDest.TryGetValue(newDir, out var dirs))
                        dirs.UnionWith(info.OldDirs);
                    else
                        sourceDirsByDest.Add(newDir, info.OldDirs.ToHashSet());
            }
            return sourceDirsByDest;
        }

        private static List<CopyOperation> CollectOperations(Dictionary<RemoteDir, HashSet<RemoteDir>> sourceDirsByDest,
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
            var operations = new List<CopyOperation>();
            foreach (var (dest, sources) in sourceDirsByDest.OrderBy(kv => kv.Key.Address.ToString()).ThenBy(kv => kv.Key.Path))
            {
                var bestSource = sources
                    .OrderByDescending(rd => dirsToDelete.Contains(rd) && loadCountByDir[rd] == 0)
                    .ThenBy(rd => loadCountByAddress[rd.Address] - (rd.Address == dest.Address ? 1 : 0))
                    .ThenBy(rd => rd.Address.ToString()).ThenBy(rd => rd.Path).First();
                loadCountByAddress[bestSource.Address]++;
                loadCountByDir[bestSource]++;
                operations.Add(new CopyOperation(bestSource, dest));
            }

            return operations;
        }

        public record struct CopyOperation(RemoteDir From, RemoteDir To);

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

        private HashSet<RemoteDir> GetDirsToDelete(List<VDiskInfo> vDiskInfo)
        {
            var result = new HashSet<RemoteDir>();
            foreach(var info in vDiskInfo)
            {
                result.UnionWith(info.OldDirs.Except(info.NewDirs));
            }
            return result;
        }

        public record struct ConfirmedDeleteOperation(RemoteDir DirToDelete, RemoteDir[] Copies);
        private List<ConfirmedDeleteOperation> GetConfirmedDeleteOperations(List<CopyOperation> copyOperations, HashSet<RemoteDir> dirsToDelete)
        {
            var result = new List<ConfirmedDeleteOperation>();
            var copiedNewByOldDir = copyOperations
                .GroupBy(t => t.From)
                .ToDictionary(g => g.Key, g => g.Select(t => t.To).Distinct().ToArray());
            foreach(var oldDir in dirsToDelete)
            {
                if (copiedNewByOldDir.TryGetValue(oldDir, out var copiedNewDirs))
                {
                    result.Add(new ConfirmedDeleteOperation(oldDir, copiedNewDirs));
                }
            }
            return result;
        }

        private List<RemoteDir> GetUnconfirmedDeleteDirs(List<CopyOperation> copyOperations, HashSet<RemoteDir> dirsToDelete)
        {
            var copied = copyOperations.Select(o => o.From).ToHashSet();
            return dirsToDelete.Except(copied).ToList();
        }

        private async Task InvokeUnconfirmedDeleteOperations(List<RemoteDir> unconfirmedDirsToRemove, CancellationToken cancellationToken)
        {
            foreach (var dir in unconfirmedDirsToRemove)
            {
                if (_args.DryRun)
                    _logger.LogInformation("Expected removing files from {Directory} (directory has no replicas)", dir);
                else
                {
                    if (await _remoteFileCopier.RemoveInDir(dir, cancellationToken))
                        _logger.LogInformation("Removed directory {From}", dir);
                    else
                        _logger.LogError("Failed to remove directory {From}", dir);
                }
            }
        }
        
        private async Task<bool> InvokeConfirmedDeleteOperations(List<ConfirmedDeleteOperation> operations, CancellationToken cancellationToken)
        {
            bool noErrors = true;
            foreach (var op in operations)
            {
                if (_args.DryRun)
                    _logger.LogInformation("Expected removing files from {Directory}", op.DirToDelete);
                else
                {
                    bool deleteAllowed = true;
                    foreach(var copy in op.Copies)
                    {
                        if (!await _remoteFileCopier.SourceCopiedToDest(op.DirToDelete, copy, cancellationToken))
                        {
                            noErrors = false;
                            _logger.LogError("Directories {From} and {To} contain different files, directory {From} can't be removed", 
                                    op.DirToDelete, copy, op.DirToDelete);
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

        private async Task<bool> Copy(CopyOperation op, CancellationToken cancellationToken)
        {
            var copyResult = await _remoteFileCopier.Copy(op.From, op.To, cancellationToken);
            if (copyResult.IsError)
                throw new OperationException($"Failed to copy data from {op.From} to {op.To}");
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
