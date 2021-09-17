using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy.Rsync
{
    public class RsyncWrapper
    {
        private readonly SshWrapper _sshWrapper;

        public RsyncWrapper(SshWrapper sshWrapper)
        {
            _sshWrapper = sshWrapper;
        }

        public async Task InvokeRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
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

            await _sshWrapper.InvokeSshProcess(from.Address, rsyncCommand.ToString(), cancellationToken);
        }
    }
}