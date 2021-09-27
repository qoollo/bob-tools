using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Rsync.Entities;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy.Rsync
{
    public class RsyncWrapper
    {
        private readonly ILogger<RsyncWrapper> _logger;
        private readonly SshWrapper _sshWrapper;

        public RsyncWrapper(ILogger<RsyncWrapper> logger, SshWrapper sshWrapper)
        {
            _logger = logger;
            _sshWrapper = sshWrapper;
        }

        public async Task<RsyncResult> InvokeRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            var sshCommandForRsyncSb = new StringBuilder(_sshWrapper.SshCommand);
            foreach (var arg in _sshWrapper.GetSshCommandAndArguments())
            {
                sshCommandForRsyncSb.Append($" {arg.First()}");
                foreach (var value in arg.Skip(1))
                    sshCommandForRsyncSb.Append($"{value}");
            }

            var _ = await _sshWrapper.InvokeSshProcess(to.Address,
                $"mkdir -p '{to.Path.TrimEnd(Path.DirectorySeparatorChar)}'", cancellationToken);

            var rsyncCommand = new StringBuilder("rsync");
            rsyncCommand.Append($" -e'{sshCommandForRsyncSb}'");
            rsyncCommand.Append(" -av");
            rsyncCommand.Append(" --exclude='*.lock'");
            // rsyncCommand.Append(" --dry-run");
            rsyncCommand.Append(" --whole-file"); // Copy whole files, without incremental updates
            rsyncCommand.Append(" --compress"); // Compress during transfer
            rsyncCommand.Append(" --out-format='f\"%f\" l\"%l\" c\"%C\"'"); // %f - filename, %l - length of the file, %C - checksum
            rsyncCommand.Append(" --checksum"); // calculate checksum before sending
            rsyncCommand.Append(" --checksum-choice=xxh128"); // checksum algorithm

            rsyncCommand.Append(" --min-size=100b"); // Larger than 100b

            rsyncCommand.Append($" '{from.Path.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}'");
            rsyncCommand.Append($" '{_sshWrapper.SshUsername}@{to.Address}:" +
                $"{to.Path.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}'");

            var sshResult = await _sshWrapper.InvokeSshProcess(from.Address, rsyncCommand.ToString(), cancellationToken);

            _logger.LogDebug("Rsync output: {rsync}", string.Join(Environment.NewLine, sshResult.StdOut));
            return new RsyncResult(sshResult);
        }
    }
}