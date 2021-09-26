using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;

namespace BobAliensRecovery.AliensRecovery
{
    public class AliensRecoverer
    {
        private readonly ILogger<AliensRecoverer> _logger;
        private readonly RemoteFileCopier _remoteFileCopier;
        private readonly PartitionInfoAggregator _partitionInfoAggregator;

        public AliensRecoverer(ILogger<AliensRecoverer> logger,
            RemoteFileCopier remoteFileCopier,
            PartitionInfoAggregator partitionInfoAggregator)
        {
            _logger = logger;
            _remoteFileCopier = remoteFileCopier;
            _partitionInfoAggregator = partitionInfoAggregator;
        }

        internal async Task RecoverAliens(
            ClusterConfiguration clusterConfiguration,
            ClusterOptions clusterOptions,
            CancellationToken cancellationToken = default)
        {
            var recoveryGroups = GetReplicas(clusterConfiguration);
            var dirs = await GetAlienDirs(clusterConfiguration, clusterOptions, cancellationToken);
            var recoveryTransactions = GetRecoveryTransactions(recoveryGroups, dirs);
            var blobsToRemove = await TransferBlobs(recoveryTransactions, cancellationToken);
            foreach (var blob in blobsToRemove)
            {
                _logger.LogInformation($"Ready to remove blob {blob}");
                await _remoteFileCopier.DeleteFiles(blob.Files, cancellationToken);
            }
        }

        private async Task<List<BlobInfo>> TransferBlobs(IEnumerable<RecoveryTransaction> recoveryTransactions, CancellationToken cancellationToken)
        {
            var blobsToRemove = new List<BlobInfo>();
            foreach (var transaction in recoveryTransactions)
            {
                var rsyncResult = await _remoteFileCopier.CopyWithRsync(transaction.From, transaction.To, cancellationToken);
                if (rsyncResult.SyncedSize > 0)
                {
                    var partitions = _partitionInfoAggregator.GetPartitionInfos(rsyncResult.SyncedFiles);
                    foreach (var partition in partitions)
                    {
                        blobsToRemove.AddRange(partition.Blobs.Where(b => b.IsClosed));
                    }
                }
            }

            return blobsToRemove;
        }

        private IEnumerable<RecoveryTransaction> GetRecoveryTransactions(IEnumerable<Replicas> recoveryGroups,
            IEnumerable<AlienDir> alienDirs)
        {
            var recoveryGroupByVdiskId = recoveryGroups.ToDictionary(rg => rg.VDiskId.ToString());
            // We check all disks as aliens are saved on any of them
            foreach (var alienSourceNode in alienDirs.SelectMany(_ => _.Children))
                foreach (var sourceVdiskDir in alienSourceNode.Children)
                {
                    var sourceRemote = new RemoteDir(sourceVdiskDir.Node.GetIPAddress(), sourceVdiskDir.Directory.Path);
                    if (recoveryGroupByVdiskId.TryGetValue(sourceVdiskDir.DirName, out var rg))
                    {
                        var targetRemote = rg.FindRemoteDirectory(alienSourceNode.DirName);
                        if (targetRemote != null)
                        {
                            var transaction = new RecoveryTransaction(sourceRemote, targetRemote);
                            _logger.LogDebug("Created {transaction}", transaction);
                            yield return transaction;
                        }
                        else
                            _logger.LogError($"Cannot find node in replicas for {sourceVdiskDir}");
                    }
                    else
                        _logger.LogError($"Cannot find recovery instructions for {sourceVdiskDir}");
                }
        }

        private IEnumerable<Replicas> GetReplicas(ClusterConfiguration clusterConfiguration)
        {
            foreach (var vdisk in clusterConfiguration.VDisks)
            {
                var remoteDirByNodeName = new Dictionary<string, RemoteDir>();
                var diskByName = vdisk.Replicas.ToDictionary(r => r.Node, r => r.Disk);
                foreach (var node in clusterConfiguration.Nodes)
                {
                    if (diskByName.TryGetValue(node.Name, out var diskName))
                    {
                        var disk = node.Disks.SingleOrDefault(d => d.Name == diskName);
                        if (disk != null)
                        {
                            var targetPath = System.IO.Path.Combine(disk.Path, "bob", vdisk.Id.ToString());
                            remoteDirByNodeName.Add(node.Name, new RemoteDir(node.GetIPAddress(), targetPath));
                        }
                        else
                            _logger.LogError($"Disk {diskName} not found on node {node.Name}");
                    }
                }
                var rg = new Replicas(vdisk.Id, remoteDirByNodeName);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Replicas for vdisk {vdisk.Id}");
                    foreach (var node in clusterConfiguration.Nodes)
                    {
                        var targetRemote = rg.FindRemoteDirectory(node.Name);
                        if (targetRemote != null)
                            sb.AppendLine($"\tNode {node.Name}: {targetRemote}");
                    }
                    _logger.LogDebug(sb.ToString());
                }

                yield return rg;
            }
        }

        private async Task<IEnumerable<AlienDir>> GetAlienDirs(ClusterConfiguration clusterConfiguration, ClusterOptions clusterOptions, CancellationToken cancellationToken)
        {
            var nodeDirectories = new HashSet<AlienDir>();
            foreach (var node in clusterConfiguration.Nodes)
            {
                try
                {
                    var bobApi = new BobApiClient(new Uri("http://" +
                        node.GetIPAddress() + ':' + clusterOptions.GetApiPort(node)));

                    var alienDiskName = await GetAlienDiskName(bobApi);
                    _logger.LogDebug($"Restarting alien disk: {alienDiskName} on {node.Name}");
                    await bobApi.StopDisk(alienDiskName);
                    await bobApi.StartDisk(alienDiskName);

                    var vdisks = await bobApi.GetVDisks(cancellationToken);
                    foreach (var vdisk in vdisks)
                    {
                        var dir = await bobApi.GetAlienDirectory();
                        nodeDirectories.Add(new AlienDir(node, dir));
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to get aliens info from node {node.Name} ({node.Address}): {e.Message}");
                }
            }

            if (_logger.IsEnabled(LogLevel.Debug))
                foreach (var nd in nodeDirectories)
                {
                    var sb = new StringBuilder();
                    DebugPrintDirectory(nd.Directory, 0, sb);
                    _logger.LogDebug($"Alien on node: {nd.Node.Address}, {Environment.NewLine}{sb}");
                }

            return nodeDirectories;
        }

        private static void DebugPrintDirectory(Directory directory, int indent, StringBuilder sink)
        {
            var indentLine = new string('\t', indent);
            sink.AppendLine($"{indentLine}{directory.Name} at {directory.Path}");
            foreach (var c in directory.Children)
                DebugPrintDirectory(c, indent + 1, sink);
        }

        private async Task<string> GetAlienDiskName(BobApiClient bobApiClient)
        {
            const string SpecialDiskName = "alien_disk";
            var disks = await bobApiClient.GetDisks();
            if (disks is null)
                throw new Exception("Failed to get disks from node");
            if (disks.Any(d => d.Name == SpecialDiskName))
                return SpecialDiskName;

            var byName = disks.GroupBy(d => d.Name);
            var multinameGroup = byName.FirstOrDefault(g => g.Count() > 1);
            if (multinameGroup != null)
                return multinameGroup.Key;

            return disks.OrderBy(d => d.Name).FirstOrDefault().Name;
        }
    }
}