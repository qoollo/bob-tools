using System.Collections.Generic;
using System.Linq;
using RemoteFileCopy.Ssh.Entities;

namespace RemoteFileCopy.Rsync.Entities
{
    public class RsyncResult
    {
        public RsyncResult(int syncedSize, SshResult sshResult)
        {
            SyncedSize = syncedSize;
            SshResult = sshResult;
        }

        public int SyncedSize { get; }
        public SshResult SshResult { get; }
        public IEnumerable<string> StdErr { get; } = Enumerable.Empty<string>();
    }
}