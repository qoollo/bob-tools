using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entites;

namespace RemoteFileCopy.RemoteFileCreation
{
    public class RemoteFileCreator
    {
        private readonly ILogger<RemoteFileCreator> _logger;

        public RemoteFileCreator(ILogger<RemoteFileCreator> logger)
        {
            _logger = logger;
        }

        public async Task<RemoteFile> CreateRemoteFile(IPEndPoint endPoint,
            string path, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Creating remote file from {endPoint}, path {path}", endPoint, path);
            var p = new Path(path);
            return await Task.FromResult(new RemoteFile(endPoint, p));
        }
    }
}