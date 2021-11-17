using System.Collections.Generic;
using DiskStatusAnalyzer.Rsync.Entities;

namespace DiskStatusAnalyzer.Entities
{
    public class AlienDir : RsyncEntry
    {
        public AlienDir(List<BobDir> nodes, RsyncEntry entry) : base(entry)
        {
            Nodes = nodes;
        }
        
        public List<BobDir> Nodes { get; }
    }
}