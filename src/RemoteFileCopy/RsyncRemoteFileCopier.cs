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
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy
{
    public class RsyncRemoteFileCopier : IRemoteFileCopier
    {
        private readonly ILogger<RsyncRemoteFileCopier> _logger;
        private readonly RsyncWrapper _rsyncWrapper;
        private readonly SshWrapper _sshWrapper;
        private readonly FilesFinder _filesFinder;

        public RsyncRemoteFileCopier(ILogger<RsyncRemoteFileCopier> logger,
            RsyncWrapper rsyncWrapper,
            SshWrapper sshWrapper,
            FilesFinder filesFinder)
        {
            _logger = logger;
            _rsyncWrapper = rsyncWrapper;
            _sshWrapper = sshWrapper;
            _filesFinder = filesFinder;
        }

        public async Task<(bool isError, string[] files)> CopyWithRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            var result = await _rsyncWrapper.InvokeRsync(from, to, cancellationToken);
            Action<string, object[]> log = _logger.LogDebug;
            if (result.SyncedSize > 0)
                log = _logger.LogInformation;
            log("Copy from {from} to {to}: {result}", new object[] { from, to, result });
            return (result.IsError, result.SyncedFiles.Select(f => f.Filename).ToArray());
        }

        public async Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            var files = await _filesFinder.FindFiles(dir, cancellationToken);
            return await RemoveFiles(files, cancellationToken)
                && await RemoveEmptySubdirs(dir, cancellationToken);
        }

        public async Task<bool> RemoveDirectory(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            var result = await _sshWrapper.InvokeSshProcess(dir.Address, $"rm -rf \"{dir.Path}\"", cancellationToken);
            return !result.IsError;
        }

        public async Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            var sshResult = await _sshWrapper.InvokeSshProcess(dir.Address,
                $"[ -d {dir.Path} ] && find {dir.Path} -type d -empty -delete", cancellationToken);
            foreach (var line in sshResult.StdErr)
                _logger.LogInformation(line);
            return !sshResult.IsError;
        }

        public async Task RemoveAlreadyMovedFiles(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            var srcFiles = await _filesFinder.FindFiles(from, cancellationToken);
            var dstFiles = await _filesFinder.FindFiles(to, cancellationToken);

            var equal = srcFiles
                .Select(f => (from, file: f))
                .ToHashSet(FileInfoComparer.Instance);
            equal.IntersectWith(dstFiles.Select(f => (to, f)));

            var filesToRemove = equal.Select(t => t.file);

            if (filesToRemove.Any())
            {
                if (await RemoveFiles(filesToRemove, cancellationToken))
                    _logger.LogInformation("Successfully removed source files from {dir}", from);
                else
                    _logger.LogWarning("Failed to remove source files from {dir}", from);
            }
        }

        internal async Task<bool> RemoveFiles(IEnumerable<RemoteFileInfo> fileInfos, CancellationToken cancellationToken = default)
        {
            var error = false;
            var filesByAddress = fileInfos.GroupBy(f => f.Address);
            foreach (var group in filesByAddress)
            {
                var content = new StringBuilder();
                content.AppendLine("'EOF'");
                foreach (var file in group)
                    content.AppendLine($"rm -f '{file.Filename}'");
                content.AppendLine("EOF");

                var sshResults = await _sshWrapper.InvokeSshProcess(group.Key, $"bash << {content}", cancellationToken);
                error |= sshResults.IsError;
            }
            return !error;
        }


        private class FileInfoComparer : IEqualityComparer<(RemoteDir dir, RemoteFileInfo file)>
        {
            public bool Equals((RemoteDir dir, RemoteFileInfo file) x, (RemoteDir dir, RemoteFileInfo file) y)
            {
                return x.file.Checksum == y.file.Checksum
                    && x.file.LengthBytes == y.file.LengthBytes
                    && x.file.Filename.AsSpan(x.dir.Path.Length).SequenceEqual(y.file.Filename.AsSpan(y.dir.Path.Length));
            }

            public int GetHashCode((RemoteDir dir, RemoteFileInfo file) obj)
            {
                return HashCode.Combine(obj.file.Checksum);
            }

            public static FileInfoComparer Instance { get; } = new();
        }
    }
}
