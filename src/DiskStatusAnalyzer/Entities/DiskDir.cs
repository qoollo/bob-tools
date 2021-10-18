using DiskStatusAnalyzer.Rsync.Entities;

namespace DiskStatusAnalyzer.Entities
{
    public class DiskDir : RsyncEntry
    {
        public DiskDir(string diskName, BobDir bob, RsyncEntry entry)
            : base(entry)
        {
            DiskName = diskName;
            Bob = bob;
        }
        public string DiskName { get; }
        public BobDir Bob { get; }

        public override string ToString()
        {
            return $"Disk: {DiskName} - {Name}";
        }
    }
}