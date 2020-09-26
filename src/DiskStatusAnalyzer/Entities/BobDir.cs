using System.Collections.Generic;
using DiskStatusAnalyzer.Rsync.Entities;

namespace DiskStatusAnalyzer.Entities
{
    public class BobDir : RsyncEntry
    {
        public BobDir(List<VDiskDir> vDisks, RsyncEntry entry) : base(entry)
        {
            VDisks = vDisks;
        }
        
        public List<VDiskDir> VDisks { get; }
    }
}