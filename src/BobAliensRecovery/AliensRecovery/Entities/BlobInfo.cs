using System.Collections.Generic;
using System.Linq;
using RemoteFileCopy.Rsync.Entities;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class BlobInfo
    {
        public BlobInfo(RsyncFileInfo blob, RsyncFileInfo? index, IEnumerable<RsyncFileInfo> files)
        {
            Blob = blob;
            Index = index;
            Files = files.ToArray();
        }

        public RsyncFileInfo Blob { get; }
        public RsyncFileInfo? Index { get; }
        public IEnumerable<RsyncFileInfo> Files { get; }

        public bool IsClosed => Index != null;

        public override string ToString()
        {
            return $"{Blob.Filename}" + (IsClosed ? ", closed" : "");
        }
    }
}