using System.Collections.Generic;
using System.Linq;
using BobApi.Entities;
using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.Rsync;
using DiskStatusAnalyzer.Rsync.Entities;
using Microsoft.Extensions.Logging;

namespace DiskStatusAnalyzer.NodeStructureCreation
{
    public class NodeStructureCreator
    {
        private readonly ILogger<NodeStructureCreator> logger;

        public NodeStructureCreator(ILogger<NodeStructureCreator> logger)
        {
            this.logger = logger;
        }

        public DiskDir ParseDisk(string diskName, Directory diskDir, ConnectionInfo connectionConfiguration)
        {
            if (diskDir.Path == null)
                return null;

            var dirs = diskDir.Children;
            var bobDir = dirs.FirstOrDefault(re => re.Name == "bob");
            if (bobDir.Path is null)
                return null;
            logger.LogDebug($"Found disk {diskName} dir {diskDir.Path}");
            var bob = ParseBob(bobDir, connectionConfiguration);
            return new DiskDir(diskName, bob, new RsyncEntry(connectionConfiguration, diskDir));
        }

        public AlienDir ParseAlien(Directory alienDir, ConnectionInfo connectionConfiguration)
        {
            if (alienDir.Path == null)
                return null;

            logger.LogDebug($"Found alien dir {alienDir.Path}");
            var dirs = alienDir.Children;
            var nodes = new List<BobDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var bob = ParseBob(dir, connectionConfiguration);
                if (bob != null)
                    nodes.Add(bob);
            }

            return new AlienDir(nodes, new RsyncEntry(connectionConfiguration, alienDir));
        }
        
        private BobDir ParseBob(Directory bobDir,
                                ConnectionInfo connectionConfiguration)
        {
            if (bobDir.Path == null)
                return null;

            logger.LogDebug($"Found bob dir {bobDir.Path}");
            var dirs = bobDir.Children;
            var vDisks = new List<VDiskDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var vDisk = ParseVDisk(dir, connectionConfiguration);
                if (vDisk != null)
                    vDisks.Add(vDisk);
            }

            return new BobDir(vDisks, new RsyncEntry(connectionConfiguration, bobDir));
        }

        private VDiskDir ParseVDisk(Directory vDiskDir,
                                    ConnectionInfo connectionConfiguration)
        {
            if (vDiskDir.Path == null
                || !int.TryParse(vDiskDir.Name, out var id))
                return null;

            logger.LogDebug($"Found vdisk dir {vDiskDir.Path}");
            var dirs = vDiskDir.Children;
            var partitions = new List<PartitionDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var partition = ParsePartition(dir, connectionConfiguration);
                if (partition != null)
                    partitions.Add(partition);
            }

            return new VDiskDir(id, partitions, new RsyncEntry(connectionConfiguration, vDiskDir));
        }

        private PartitionDir ParsePartition(Directory partitionDir,
                                            ConnectionInfo connectionConfiguration)
        {
            PartitionDir result = null;
            if (partitionDir.Path != null)
            {
                logger.LogDebug($"Found partition dir {partitionDir.Path}");
                result = new PartitionDir(partitionDir.Name, new RsyncEntry(connectionConfiguration,
                                                                            partitionDir));
            }
            return result;
        }

    }
}
