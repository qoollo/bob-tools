using System.Collections.Generic;
using System.Linq;
using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.Rsync;
using DiskStatusAnalyzer.Rsync.Entities;
using Microsoft.Extensions.Logging;

namespace DiskStatusAnalyzer.NodeStructureCreation
{
    public class NodeStructureCreator
    {
        private readonly RsyncWrapper rsyncWrapper;
        private readonly ILogger<NodeStructureCreator> logger;

        public NodeStructureCreator(RsyncWrapper rsyncWrapper, ILogger<NodeStructureCreator> logger)
        {
            this.rsyncWrapper = rsyncWrapper;
            this.logger = logger;
        }

        public DiskDir ParseDisk(TreeParser.Entry diskDir)
        {
            if (diskDir?.IsDir != true)
                return null;

            var dirs = diskDir.Children;
            var bobDir = dirs.FirstOrDefault(re => re.Name == "bob");
            if (bobDir is null)
                return null;
            logger.LogDebug($"Found disk dir {diskDir.Path}");
            var bob = ParseBob(bobDir);
            var alienDir = dirs.FirstOrDefault(re => re.Name == "alien");
            var alien = ParseAlien(alienDir);
            return new DiskDir(diskDir.Name, bob, alien, new RsyncEntry(rsyncWrapper, diskDir));
        }

        private BobDir ParseBob(TreeParser.Entry bobDir)
        {
            if (!bobDir.IsDir)
                return null;

            logger.LogDebug($"Found bob dir {bobDir.Path}");
            var dirs = bobDir.Children;
            var vDisks = new List<VDiskDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var vDisk = ParseVDisk(dir);
                if (vDisk != null)
                    vDisks.Add(vDisk);
            }

            return new BobDir(vDisks, new RsyncEntry(rsyncWrapper, bobDir));
        }

        private VDiskDir ParseVDisk(TreeParser.Entry vDiskDir)
        {
            if (vDiskDir?.IsDir != true
                || !int.TryParse(vDiskDir.Name, out var id))
                return null;

            logger.LogDebug($"Found vdisk dir {vDiskDir.Path}");
            var dirs = vDiskDir.Children;
            var partitions = new List<PartitionDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var partition = ParsePartition(dir);
                if (partition != null)
                    partitions.Add(partition);
            }

            return new VDiskDir(id, partitions, new RsyncEntry(rsyncWrapper, vDiskDir));
        }

        private PartitionDir ParsePartition(TreeParser.Entry partitionDir)
        {
            PartitionDir result = null;
            if (partitionDir?.IsDir == true)
            {
                logger.LogDebug($"Found partition dir {partitionDir.Path}");
                result = new PartitionDir(partitionDir.Name, new RsyncEntry(rsyncWrapper, partitionDir));
            }
            return result;
        }

        private AlienDir ParseAlien(TreeParser.Entry alienDir)
        {
            if (alienDir?.IsDir != true)
                return null;

            logger.LogDebug($"Found alien dir {alienDir.Path}");
            var dirs = alienDir.Children;
            var nodes = new List<BobDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var bob = ParseBob(dir);
                if (bob != null)
                    nodes.Add(bob);
            }

            return new AlienDir(nodes, new RsyncEntry(rsyncWrapper, alienDir));
        }
    }
}
