using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Entities;

namespace RemoteFileCopy
{
    public interface IRemoteFileCopier
    {
        Task<CopyResult> Copy(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default);

        Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default);

        Task<bool> RemoveDirectory(RemoteDir dir, CancellationToken cancellationToken = default);

        Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default);

        Task<int> RemoveAlreadyMovedFiles(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default);

        Task<bool> SourceCopiedToDest(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default);
    }
}
