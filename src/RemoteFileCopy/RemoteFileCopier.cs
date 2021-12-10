using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
            var result = await _rsyncWrapper.InvokeRsync(from, to, cancellationToken);
            Action<string, object[]> log = _logger.LogDebug;
            if (result.SyncedSize > 0)
                log = _logger.LogInformation;
            log("Copy from {from} to {to}: {result}", new object[] { from, to, result });
            return result;
        }

        public async Task<bool> RemoveFiles(IEnumerable<RemoteFileInfo> fileInfos, CancellationToken cancellationToken = default)
        {
            var error = false;
            var filesByAddress = fileInfos.GroupBy(f => f.Address);
            foreach (var group in filesByAddress)
            {
                // var scriptFile = Path.GetTempFileName();
                var scriptFile = Path.Combine(Directory.GetCurrentDirectory(), "list_files_2.sh");
                var content = new StringBuilder();
                content.AppendLine("#!/bin/sh");
                foreach (var file in group)
                    content.AppendLine($"rm -f '{file.Filename}'");
                await File.WriteAllTextAsync(scriptFile, content.ToString(), cancellationToken: cancellationToken);
                try
                {
                    var sshResults = await _sshWrapper.InvokeSshProcess(group.Key, $"sh -s < {scriptFile}", cancellationToken);
                    error |= sshResults.IsError;
                }
                finally
                {
                    File.Delete(scriptFile);
                }
            }
            return !error;
        }

        public async Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            var sshResult = await _sshWrapper.InvokeSshProcess(dir.Address,
                $"[ -d {dir.Path} ] && find {dir.Path} -type d -empty -delete", cancellationToken);
            foreach (var line in sshResult.StdErr)
                _logger.LogInformation(line);
            return !sshResult.IsError;
        }
    }
}
