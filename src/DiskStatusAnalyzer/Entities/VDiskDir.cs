using System.Collections.Generic;
using DiskStatusAnalyzer.Rsync.Entities;

namespace DiskStatusAnalyzer.Entities
{
    public class VDiskDir : RsyncEntry
    {
        public VDiskDir(int id, List<PartitionDir> partitions, RsyncEntry entry)
            : base(entry)
        {
            Id = id;
            Partitions = partitions;
        }
        public int Id { get; }
        public List<PartitionDir> Partitions { get; }

        public override string ToString()
        {
            return $"VDisk {Id} - {Name}";
        }
    }
}