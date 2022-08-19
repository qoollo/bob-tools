using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Rsync.Entities;

namespace RemoteFileCopy
{
    public interface IRemoteFileCopier
    {
        Task<RsyncResult> CopyWithRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default);

        Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default);

        Task<bool> RemoveDirectory(RemoteDir dir, CancellationToken cancellationToken = default);

        Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default);

        Task RemoveAlreadyMovedFiles(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default);
    }
}
