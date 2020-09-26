using DiskStatusAnalyzer.Rsync.Entities;

namespace DiskStatusAnalyzer.Entities
{
    public class PartitionDir : RsyncEntry
    {
        public PartitionDir(string diskName, RsyncEntry entry) : base(entry)
        {
            DiskName = diskName;
        }
        public string DiskName { get; }

        public override string ToString()
        {
            return $"Partition: {DiskName} - {Name}";
        }
    }
}