using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;
using RemoteFileCopy.FilesFinding;
using RemoteFileCopy.Rsync;
using RemoteFileCopy.Rsync.Entities;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy
{
    public interface IRemoteFileCopier
    {
        Task<RsyncResult> CopyWithRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default);

        Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default);

        Task<bool> RemoveFiles(IEnumerable<RemoteFileInfo> fileInfos, CancellationToken cancellationToken = default);

        Task<bool> RemoveDirectory(RemoteDir dir, CancellationToken cancellationToken = default);

        Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default);
    }
}
