using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BobApi.BobEntities;
using Microsoft.Extensions.Logging;

namespace ClusterModifier
{
    public class ClusterExpander
    {
        private readonly ILogger<ClusterExpander> _logger;

        public ClusterExpander(ILogger<ClusterExpander> logger)
        {
            _logger = logger;
        }

        public async Task ExpandCluster(ClusterExpandArguments args)
        {
            _logger.LogDebug("Expanding cluster from {OldConfigPath} to {NewConfigPath}",
                args.OldConfigPath, args.ClusterConfigPath);
            var oldConfig = await ClusterConfiguration.FromYamlFile(args.OldConfigPath);
            var newConfig = await ClusterConfiguration.FromYamlFile(args.ClusterConfigPath);
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
                                if (CopyReplica(vdisk, replica, selectedReplica, oldDisk.Path, disk.Path, args))
                                    break;
                                else
                                    _logger.LogWarning("Failed to copy from replica on node {Node}", selectedReplica.Node);
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
                        RemoveReplica(replica, vDisk, disk.Path, args);
                    }
                }
        }

        private bool RemoveReplica(ClusterConfiguration.VDisk.Replica replica,
            ClusterConfiguration.VDisk vDisk,
            string path,
            ClusterExpandArguments args)
        {
            var dsaPath = Path.GetFullPath(args.DiskStatusAnalyzer);
            var startInfo = new ProcessStartInfo
            {
                FileName = dsaPath,
                WorkingDirectory = Path.GetDirectoryName(dsaPath),
            };
            startInfo.ArgumentList.Add("remove-dir");
            startInfo.ArgumentList.Add($"--node");
            startInfo.ArgumentList.Add($"{replica.Node}");
            startInfo.ArgumentList.Add($"--dir");
            startInfo.ArgumentList.Add($"{path}{Path.DirectorySeparatorChar}bob{Path.DirectorySeparatorChar}{vDisk.Id}");
            var process = new Process { StartInfo = startInfo };
            _logger.LogInformation("Starting process (pwd={WorkingDirectory}) {FileName} {ArgumentList}",
                startInfo.WorkingDirectory, startInfo.FileName, string.Join(" ", process.StartInfo.ArgumentList));
            if (args.DryRun)
                return true;
            process.Start();
            process.WaitForExit();
            _logger.LogInformation("Process returned code {ExitCode}", process.ExitCode);
            return process.ExitCode == 0;
        }

        private bool CopyReplica(
            ClusterConfiguration.VDisk vdisk,
            ClusterConfiguration.VDisk.Replica replica,
            ClusterConfiguration.VDisk.Replica oldReplica,
            string oldPath,
            string newPath,
            ClusterExpandArguments args)
        {
            var dsaPath = Path.GetFullPath(args.DiskStatusAnalyzer);
            var startInfo = new ProcessStartInfo
            {
                FileName = dsaPath,
                WorkingDirectory = Path.GetDirectoryName(dsaPath),
            };
            startInfo.ArgumentList.Add("copy-dir");
            startInfo.ArgumentList.Add($"-s");
            startInfo.ArgumentList.Add($"{oldReplica.Node}");
            startInfo.ArgumentList.Add($"-d");
            startInfo.ArgumentList.Add($"{replica.Node}");
            startInfo.ArgumentList.Add($"--source-dir");
            startInfo.ArgumentList.Add($"{oldPath}{Path.DirectorySeparatorChar}bob{Path.DirectorySeparatorChar}{vdisk.Id}");
            startInfo.ArgumentList.Add($"--dest-dir");
            startInfo.ArgumentList.Add($"{newPath}{Path.DirectorySeparatorChar}bob{Path.DirectorySeparatorChar}{vdisk.Id}");
            var process = new Process { StartInfo = startInfo };
            _logger.LogInformation("Starting process (pwd={WorkingDirectory}) {FileName} {ArgumentList}",
                startInfo.WorkingDirectory, startInfo.FileName, string.Join(" ", process.StartInfo.ArgumentList));
            if (args.DryRun)
                return true;
            process.Start();
            process.WaitForExit();
            _logger.LogInformation("Process returned code {ExitCode}", process.ExitCode);
            return process.ExitCode == 0;
        }

    }
}