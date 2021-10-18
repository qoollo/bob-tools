using System.Collections.Generic;
using DiskStatusAnalyzer.Rsync.Entities;

namespace DiskStatusAnalyzer.Entities
{
    public class NodeDir : RsyncEntry
    {
        public NodeDir(List<DiskDir> disks, RsyncEntry entry) : base(entry)
        {
            Disks = disks;
        }
        
        public List<DiskDir> Disks { get; }
    }
}