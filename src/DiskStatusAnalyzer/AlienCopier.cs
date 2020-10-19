using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.Rsync;
using Microsoft.Extensions.Logging;

namespace DiskStatusAnalyzer
{
    public class AlienCopier
    {
        private readonly ILogger<AlienCopier> logger;
        private readonly RsyncWrapper rsyncWrapper;

        public AlienCopier(ILogger<AlienCopier> logger, RsyncWrapper rsyncWrapper)
        {
            this.logger = logger;
            this.rsyncWrapper = rsyncWrapper;
        }

        public async Task CopyAlienInformation(List<Node> nodes, Configuration config)
        {
            foreach (var node in nodes)
            {
                logger.LogInformation($"Searching for aliens on {node}");
                await CopyAliensFromNode(node, nodes, config);
            }
        }

        private async Task CopyAliensFromNode(Node node, List<Node> nodes, Configuration config)
        {
            foreach (var diskDir in node.DiskDirs)
            {
                if (diskDir.Alien == null) continue;
                foreach (var alienNode in diskDir.Alien.Nodes)
                {
                    await CopyAlien(alienNode, nodes, config);
                }
            }
        }

        private async Task CopyAlien(BobDir alienNode, List<Node> nodes, Configuration config)
        {
            var targetNode = nodes.FirstOrDefault(n => n.Name == alienNode.Name);
            logger.LogInformation($"Found target {targetNode} for alien");
            if (targetNode == null) return;
            foreach (var vDisk in alienNode.VDisks)
            {
                await CopyAlienFromVDisk(vDisk, config, targetNode);

                if (config.RemoveCopiedFiles)
                    await RemoveCopiedData(vDisk);
            }
        }

        private async Task CopyAlienFromVDisk(VDiskDir vDisk, Configuration config, Node targetNode)
        {
            var targetVDisk = GetTargetVDisk(vDisk, targetNode);
            logger.LogInformation($"Checking copy from {vDisk} to {targetVDisk}");
            if (ContainsNonCopiedPartition(vDisk, targetVDisk))
            {
                logger.LogInformation($"Found not copied partition on {vDisk}");
                if (config.CopyAliens)
                {
                    if (!await rsyncWrapper.Copy(vDisk, targetVDisk))
                        logger.LogError($"Error copying {vDisk} to {targetVDisk}");
                    else
                    {
                        logger.LogInformation($"Successfully copied partitions from {vDisk}");
                        if (config.RestartAfterCopy)
                        {
                            await RestartVDisk(vDisk, targetNode);
                        }
                    }
                }
            }
        }

        private async Task RemoveCopiedData(VDiskDir vDisk)
        {
            var filesInDir = await rsyncWrapper.FindFilesWithUniqueIdRelative(vDisk);
            var syncedFiles = await rsyncWrapper.FindSyncedFiles(vDisk);

            foreach (var file in filesInDir)
                logger.LogInformation($"In dir: {file}");
            foreach (var file in syncedFiles)
                logger.LogInformation($"Synced: {file}");

            var filesToRemove = new List<string>();
            foreach (var syncedFile in syncedFiles)
            {
                var pathIndex = syncedFile.IndexOf(RsyncWrapper.PathStart) + RsyncWrapper.PathStart.Length;
                if (pathIndex < syncedFile.Length)
                {
                    var filename = syncedFile.Substring(pathIndex).Trim();
                    if (filesInDir.Contains(syncedFile))
                    {
                        logger.LogInformation($"File {filename} is marked for removal");
                        filesToRemove.Add(filename);
                    }
                }
                else
                {
                    logger.LogWarning($"Received strange file string: {syncedFile}");
                }
            }

            if (filesToRemove.Count > 0)
            {
                logger.LogInformation($"Removing files {string.Join(", ", filesToRemove)}");
                if (!await rsyncWrapper.RemoveFiles(vDisk,

                                                    filesToRemove.Select(f => Path.Combine(vDisk.Path, f.Trim('/')))))
                    logger.LogError($"Failed to remove files");
            }
        }

        private bool ContainsNonCopiedPartition(VDiskDir vDisk, VDiskDir targetVDisk)
        {
            if (targetVDisk?.Partitions != null && vDisk?.Partitions != null)
                return vDisk.Partitions.Any(partition => targetVDisk.Partitions.All(p => p.Name != partition.Name));
            return false;
        }

        private VDiskDir GetTargetVDisk(VDiskDir vDisk, Node targetNode)
        {
            return targetNode.DiskDirs
                .SelectMany(d => d.Bob.VDisks.Where(vd => vd.Id == vDisk.Id)).FirstOrDefault();
        }

        private async Task RestartVDisk(VDiskDir vDisk, Node targetNode)
        {
            logger.LogInformation($"Restarting vdisk {vDisk.Id} on node {targetNode.Name}");
            using var client = new HttpClient { BaseAddress = targetNode.Uri };
            var res = await client.PostAsync(
                $"vdisks/{vDisk.Id}/remount",
                new StringContent(string.Empty));
            if (!res.IsSuccessStatusCode)
                logger.LogError($"Failed to restart vdisk {vDisk.Id} on node {targetNode.Name}");
        }
    }
}
