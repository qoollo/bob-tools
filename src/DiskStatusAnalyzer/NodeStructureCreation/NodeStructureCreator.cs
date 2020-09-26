using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.Rsync;
using DiskStatusAnalyzer.Rsync.Entities;

namespace DiskStatusAnalyzer.NodeStructureCreation
{
    public class NodeStructureCreator
    {
        private readonly RsyncWrapper rsyncWrapper;

        public NodeStructureCreator(RsyncWrapper rsyncWrapper)
        {
            this.rsyncWrapper = rsyncWrapper;
        }

        public async Task<NodeDir> CreateNodeDir(RsyncEntry baseEntry)
        {
            if (baseEntry?.IsDirectory != true)
                return null;
            
            var nodeDirs = await rsyncWrapper.GetDirs(baseEntry.Path);
            var disks = new List<DiskDir>(nodeDirs.Count);
            foreach (var dir in nodeDirs)
            {
                var disk = await ParseDisk(dir);
                if (disk != null)
                    disks.Add(disk);
            }

            return new NodeDir(disks, baseEntry);
        }

        public async Task<DiskDir> ParseDisk(RsyncEntry diskDir)
        {
            if (diskDir?.IsDirectory != true)
                return null;
            
            var dirs = await rsyncWrapper.GetDirs(diskDir.Path);
            var bobDir = dirs.FirstOrDefault(re => re.Name == "bob");
            if (bobDir is null)
                return null;
            var bob = await ParseBob(bobDir);
            var alienDir = dirs.FirstOrDefault(re => re.Name == "alien");
            var alien = await ParseAlien(alienDir);
            return new DiskDir(diskDir.Name, bob, alien, diskDir);
        }

        private async Task<BobDir> ParseBob(RsyncEntry bobDir)
        {
            if (bobDir?.IsDirectory != true)
                return null;
            
            var dirs = await rsyncWrapper.GetDirs(bobDir.Path);
            var vDisks = new List<VDiskDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var vDisk = await ParseVDisk(dir);
                if (vDisk != null)
                    vDisks.Add(vDisk);
            }

            return new BobDir(vDisks, bobDir);
        }

        private async Task<VDiskDir> ParseVDisk(RsyncEntry vDiskDir)
        {
            if (vDiskDir?.IsDirectory != true
                || !int.TryParse(vDiskDir.Name, out var id))
                return null;

            var dirs = await rsyncWrapper.GetDirs(vDiskDir.Path);
            var partitions = new List<PartitionDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var partition = await ParsePartition(dir);
                if (partition != null)
                    partitions.Add(partition);
            }

            return new VDiskDir(id, partitions, vDiskDir);
        }

        private Task<PartitionDir> ParsePartition(RsyncEntry partitionDir)
        {
            PartitionDir result = null;
            if (partitionDir?.IsDirectory == true)
                result = new PartitionDir(partitionDir.Name, partitionDir);

            return Task.FromResult(result);
        }

        private async Task<AlienDir> ParseAlien(RsyncEntry alienDir)
        {
            if (alienDir?.IsDirectory != true)
                return null;

            var dirs = await rsyncWrapper.GetDirs(alienDir.Path);
            var nodes = new List<BobDir>(dirs.Count);
            foreach (var dir in dirs)
            {
                var bob = await ParseBob(dir);
                if (bob != null)
                    nodes.Add(bob);
            }

            return new AlienDir(nodes, alienDir);
        }
    }
}