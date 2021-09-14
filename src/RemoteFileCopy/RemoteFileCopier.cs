using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entites;

namespace RemoteFileCopy
{
    public class RemoteFileCopier
    {
        private readonly ILogger<RemoteFileCopier> _logger;

        public RemoteFileCopier(ILogger<RemoteFileCopier> logger)
        {
            _logger = logger;
        }

        public async Task<CopyResult> Copy(RemoteFile from, RemoteFile to, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Copy from {from} to {to}", from, to);

            return await Task.FromResult(new CopyResult());
        }
    }
}
