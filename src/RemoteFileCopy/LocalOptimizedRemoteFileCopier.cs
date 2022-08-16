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
    public class LocalOptimizedRemoteFileCopier : IRemoteFileCopier
    {
        private readonly IRemoteFileCopier _remoteFileCopier;

        public LocalOptimizedRemoteFileCopier(IRemoteFileCopier remoteFileCopier)
        {
            _remoteFileCopier = remoteFileCopier;
        }

        public async Task<RsyncResult> CopyWithRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.CopyWithRsync(from, to, cancellationToken);
        }

        public async Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.RemoveInDir(dir, cancellationToken);
        }

        public async Task<bool> RemoveFiles(IEnumerable<RemoteFileInfo> fileInfos, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.RemoveFiles(fileInfos, cancellationToken);
        }

        public async Task<bool> RemoveDirectory(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.RemoveDirectory(dir, cancellationToken);
        }

        public async Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.RemoveEmptySubdirs(dir, cancellationToken);
        }
    }
}
