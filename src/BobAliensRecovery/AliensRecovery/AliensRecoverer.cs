using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobApi.BobEntities;
using BobApi.Entities;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery.AliensRecovery
{
    public class AliensRecoverer
    {
        private readonly ILogger<AliensRecoverer> _logger;

        public AliensRecoverer(ILogger<AliensRecoverer> logger)
        {
            _logger = logger;
        }

        internal async Task RecoverAliens(
            ClusterConfiguration clusterConfiguration,
            ClusterOptions clusterOptions,
            CancellationToken cancellationToken = default)
        {
            var recoveryGroups = GetRecoveryGroups(clusterConfiguration);
            var dirs = await GetNodeDiskDirs(clusterConfiguration, clusterOptions, cancellationToken);
            var recoveryTransactions = GetRecoveryTransactions(recoveryGroups, dirs, clusterConfiguration);

            _logger.LogInformation($"Found {recoveryTransactions.Count()} recovery transactions");
        }

        private IEnumerable<RecoveryTransaction> GetRecoveryTransactions(IEnumerable<RecoveryGroup> recoveryGroups,
            IEnumerable<NodeDiskDir> dirs, ClusterConfiguration clusterConfiguration)
        {
            var recoveryGroupByVdiskId = recoveryGroups.ToDictionary(rg => rg.VDiskId.ToString());
            // We check all disks as aliens are saved on any of them
            foreach (var nd in dirs)
            {
                var alienDir = nd.GetMatchedDirs(d => d.Directory.Name == "alien").SingleOrDefault();
                if (alienDir != null)
                    foreach (var alienSourceNode in alienDir.Children)
                    {
                        foreach (var sourceVdiskDir in alienSourceNode.Children)
                        {
                            if (recoveryGroupByVdiskId.TryGetValue(sourceVdiskDir.Directory.Name, out var rg))
                            {
                                if (rg.DiskByNodeName.TryGetValue(alienSourceNode.Directory.Name, out var diskName))
                                {
                                    var targetNd = dirs.SingleOrDefault(_ => _.Node.Name == alienSourceNode.Directory.Name
                                        && rg.DiskByNodeName.TryGetValue(_.Node.Name, out var dn)
                                        && dn == _.DiskName);

                                    var targetNode = clusterConfiguration.Nodes.Single(n => n.Name == alienSourceNode.Directory.Name);
                                    var targetDisk = targetNode.Disks.Single(d => d.Name == diskName);
                                    var targetEP = IPEndPoint.Parse(targetNode.Address);
                                    var targetPath = System.IO.Path.Combine(targetDisk.Path, "bob", sourceVdiskDir.Directory.Name);
                                    var targetRemote = new RemoteDirectory(targetEP.Address, targetPath);
                                    var sourceEP = IPEndPoint.Parse(sourceVdiskDir.Node.Address);
                                    var sourceRemote = new RemoteDirectory(sourceEP.Address, sourceVdiskDir.Directory.Path);
                                    var t = new RecoveryTransaction(sourceRemote, targetRemote);
                                    _logger.LogInformation("Created {transaction}", t);
                                    yield return t;
                                }
                                else
                                    _logger.LogError($"Cannot find node in replicas for {sourceVdiskDir}");
                            }
                            else
                                _logger.LogError($"Cannot find recovery instructions for {sourceVdiskDir}");
                        }
                    }
            }
        }

        private IEnumerable<RecoveryGroup> GetRecoveryGroups(ClusterConfiguration clusterConfiguration)
        {
            foreach (var vdisk in clusterConfiguration.VDisks)
            {
                var rg = new RecoveryGroup(vdisk.Id, vdisk.Replicas.ToDictionary(r => r.Node, r => r.Disk));
                _logger.LogDebug($"Recovery group for vdisk {rg.VDiskId}");
                _logger.LogDebug($"Involved nodes: {string.Join(", ", rg.DiskByNodeName.Select(kv => $"{kv.Key}={kv.Value}"))}");

                yield return rg;
            }
        }

        private async Task<IEnumerable<NodeDiskDir>> GetNodeDiskDirs(ClusterConfiguration clusterConfiguration, ClusterOptions clusterOptions, CancellationToken cancellationToken)
        {
            var nodeDirectories = new HashSet<NodeDiskDir>();
            foreach (var node in clusterConfiguration.Nodes)
            {
                var ep = IPEndPoint.Parse(node.Address);
                ep.Port = clusterOptions.GetApiPort(node);

                try
                {
                    var bobApi = new BobApi.BobApiClient(new System.Uri("http://" + ep));
                    var vdisks = await bobApi.GetVDisks(cancellationToken);
                    foreach (var vdisk in vdisks)
                    {
                        var directories = await bobApi.GetDirectories(vdisk, cancellationToken);
                        foreach (var dir in directories)
                        {
                            var disk = node.Disks.SingleOrDefault(d => d.Path == dir.Path);
                            if (disk != null)
                                nodeDirectories.Add(new NodeDiskDir(node, disk.Name, dir));
                            else
                                _logger.LogError($"Disk {dir.Path} not found in {node.Name} config");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to get directory info from node {ep}: {e.Message}");
                }
            }

            foreach (var nd in nodeDirectories)
            {
                var sb = new StringBuilder();
                DebugPrintDirectory(nd.Directory, 0, sb);
                _logger.LogDebug($"Node: {nd.Node.Address}, disk: {nd.DiskName}{Environment.NewLine}{sb}");
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
    }
}