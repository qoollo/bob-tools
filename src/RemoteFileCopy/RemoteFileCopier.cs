﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Rsync;
using RemoteFileCopy.Rsync.Entities;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy
{
    public class RemoteFileCopier
    {
        private readonly ILogger<RemoteFileCopier> _logger;
        private readonly RsyncWrapper _rsyncWrapper;
        private readonly SshWrapper _sshWrapper;

        public RemoteFileCopier(ILogger<RemoteFileCopier> logger,
            RsyncWrapper rsyncWrapper,
            SshWrapper sshWrapper)
        {
            _logger = logger;
            _rsyncWrapper = rsyncWrapper;
            _sshWrapper = sshWrapper;
        }

        public async Task<RsyncResult> CopyWithRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug($"Copying files from {from} to {to}");

            var result = await _rsyncWrapper.InvokeRsync(from, to, cancellationToken);
            if (result.SyncedSize > 0)
                _logger.LogInformation($"Copy from {from} to {to}: transfered {result.SyncedSize} bytes");
            return result;
        }

        public async Task<bool> RemoveFiles(IEnumerable<RsyncFileInfo> fileInfos, CancellationToken cancellationToken = default)
        {
            var error = false;
            foreach (var file in fileInfos)
            {
                var sshResults = await _sshWrapper.InvokeSshProcess(file.Address, $"rm -f '{file.Filename}'", cancellationToken);
                error |= sshResults.StdErr.Any();
            }
            return !error;
        }

        public async Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            var sshResult = await _sshWrapper.InvokeSshProcess(dir.Address, $"find {dir.Path} -type d -empty -delete", cancellationToken);
            return !sshResult.StdErr.Any();
        }
    }
}
