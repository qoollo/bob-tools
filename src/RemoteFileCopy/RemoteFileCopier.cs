using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Rsync;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy
{
    public class RemoteFileCopier
    {
        private readonly ILogger<RemoteFileCopier> _logger;
        private readonly RsyncWrapper _rsyncWrapper;

        public RemoteFileCopier(ILogger<RemoteFileCopier> logger,
            RsyncWrapper rsyncWrapper)
        {
            _logger = logger;
            _rsyncWrapper = rsyncWrapper;
        }

        public async Task Copy(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"Copying files from {from} to {to}");


            await _rsyncWrapper.InvokeRsync(from, to, cancellationToken);
        }
    }
}
