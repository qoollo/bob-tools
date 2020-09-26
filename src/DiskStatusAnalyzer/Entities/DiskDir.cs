using DiskStatusAnalyzer.Rsync.Entities;

namespace DiskStatusAnalyzer.Entities
{
    public class DiskDir : RsyncEntry
    {
        public DiskDir(string diskName, BobDir bob, AlienDir alien, RsyncEntry entry)
            : base(entry)
        {
            DiskName = diskName;
            Bob = bob;
            Alien = alien;
        }
        public string DiskName { get; }
        public BobDir Bob { get; }
        public AlienDir Alien { get; }

        public override string ToString()
        {
            return $"Disk: {DiskName} - {Name}";
        }
    }
}