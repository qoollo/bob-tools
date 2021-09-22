using System;
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
        private static readonly Regex s_totalSizeRegex = new Regex(@"total size is (\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly ILogger<RsyncWrapper> _logger;
        private readonly SshWrapper _sshWrapper;

        public RsyncWrapper(ILogger<RsyncWrapper> logger, SshWrapper sshWrapper)
        {
            _logger = logger;
            _sshWrapper = sshWrapper;
        }

        public async Task<RsyncResult?> InvokeRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            var sshCommandForRsyncSb = new StringBuilder(_sshWrapper.SshCommand);
            foreach (var arg in _sshWrapper.GetSshCommandAndArguments())
            {
                sshCommandForRsyncSb.Append($" {arg.First()}");
                foreach (var value in arg.Skip(1))
                    sshCommandForRsyncSb.Append($"{value}");

            }

            var rsyncCommand = new StringBuilder("rsync");
            rsyncCommand.Append($" -e'{sshCommandForRsyncSb}'");
            rsyncCommand.Append(" -av");
            rsyncCommand.Append(" --remove-source-files");
            rsyncCommand.Append(" --exclude='*.lock'");
            rsyncCommand.Append($" '{from.Path}'");
            rsyncCommand.Append($" '{_sshWrapper.SshUsername}@{to.Address}:{to.Path}'");

            var sshResult = await _sshWrapper.InvokeSshProcess(from.Address, rsyncCommand.ToString(), cancellationToken);

            var totalSizeLine = sshResult.StdOut.FirstOrDefault(s_totalSizeRegex.IsMatch);
            if (totalSizeLine != null)
            {
                var size = int.Parse(s_totalSizeRegex.Match(totalSizeLine).Groups[1].Value);
                _logger.LogDebug("Rsync sent size: {size}", size);
                return new RsyncResult(size, sshResult);
            }

            _logger.LogError($"Error sending data from {from} to {to}:{Environment.NewLine}{string.Join(Environment.NewLine, sshResult.StdErr)}");
            return null;
        }
    }
}