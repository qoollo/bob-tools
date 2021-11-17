using System.Collections.Generic;
using System.Linq;
using RemoteFileCopy.Rsync.Entities;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class PartitionInfo
    {
        public PartitionInfo(int vDiskId, string name, IEnumerable<BlobInfo> blobs)
        {
            VDiskId = vDiskId;
            Name = name;
            Blobs = blobs.ToArray();
        }

        public int VDiskId { get; }
        public string Name { get; }
        public IEnumerable<BlobInfo> Blobs { get; }

        public override string ToString() => $"{VDiskId} - {Name}, {Blobs.Count()} blobs, {Blobs.Count(b => b.IsClosed)} closed";

    }
}