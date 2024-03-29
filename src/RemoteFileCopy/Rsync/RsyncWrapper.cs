using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.DependenciesChecking;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Exceptions;
using RemoteFileCopy.Rsync.Entities;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy.Rsync
{
    public class RsyncWrapper
    {
        private const string RsyncExecutable = "rsync";

        private readonly ILogger<RsyncWrapper> _logger;
        private readonly SshWrapper _sshWrapper;
        private readonly RemoteDependenciesChecker _remoteDependenciesChecker;

        public RsyncWrapper(ILogger<RsyncWrapper> logger, SshWrapper sshWrapper,
            RemoteDependenciesChecker remoteDependenciesChecker)
        {
            _logger = logger;
            _sshWrapper = sshWrapper;
            _remoteDependenciesChecker = remoteDependenciesChecker;
        }

        internal async Task<RsyncResult> InvokeRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            await CreateDir(to, cancellationToken);

            return await CopyFiles(from, to, cancellationToken);
        }

        private async Task<RsyncResult> CopyFiles(RemoteDir from, RemoteDir to, CancellationToken cancellationToken)
        {
            if (!await _remoteDependenciesChecker.SshConnectionExists(from.Address, cancellationToken))
                throw new MissingDependencyException($"Sshd on {from.Address}");

            if (!await _remoteDependenciesChecker.RemoteProgramExists(from.Address, RsyncExecutable, cancellationToken))
                throw new MissingDependencyException($"Rsync on {from.Address}");

            var rsyncCommand = new StringBuilder(RsyncExecutable);
            var sshCommandForRsyncSb = new StringBuilder(_sshWrapper.SshCommand + " ");
            sshCommandForRsyncSb.Append(string.Join(" ", _sshWrapper.GetSshCommandAndArguments(true)));

            rsyncCommand.Append($" -e '{sshCommandForRsyncSb}'");
            rsyncCommand.Append(" -av");
            rsyncCommand.Append(" --exclude='*.lock'");
            // rsyncCommand.Append(" --dry-run");
            rsyncCommand.Append(" --whole-file"); // Copy whole files, without incremental updates
            rsyncCommand.Append(" --compress"); // Compress during transfer
            rsyncCommand.Append(" --out-format='f\"%f\" l\"%l\" c\"%C\"'"); // %f - filename, %l - length of the file, %C - checksum
            rsyncCommand.Append(" --checksum"); // calculate checksum before sending
            // rsyncCommand.Append(" --checksum-choice=xxh128"); // checksum algorithm

            // rsyncCommand.Append(" --min-size=20"); // Larger than 100b

            rsyncCommand.Append($" '{from.Path.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}'");
            rsyncCommand.Append($" '{_sshWrapper.SshUsername}@{to.Address}:" +
                $"{to.Path.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}'");

            var sshResult = await _sshWrapper.InvokeSshProcess(from.Address, rsyncCommand.ToString(), cancellationToken);

            _logger.LogDebug("Rsync output: {rsync}", string.Join(Environment.NewLine, sshResult.StdOut));
            if (sshResult.StdErr.Any())
                _logger.LogWarning("Rsync stderr: {rsync}", string.Join(Environment.NewLine, sshResult.StdErr));
            return new RsyncResult(sshResult);
        }

        private async Task CreateDir(RemoteDir to, CancellationToken cancellationToken)
        {
            var _ = await _sshWrapper.InvokeSshProcess(to.Address,
                $"mkdir -p '{to.Path.TrimEnd(Path.DirectorySeparatorChar)}'", cancellationToken);
        }
    }
}
