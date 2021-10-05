using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy.DependenciesChecking
{
    public class RemoteDependenciesChecker
    {
        private readonly SshWrapper _sshWrapper;

        public RemoteDependenciesChecker(SshWrapper sshWrapper)
        {
            _sshWrapper = sshWrapper;
        }

        public async Task<bool> SshConnectionExists(IPAddress address, CancellationToken cancellationToken = default)
        {
            var result = await _sshWrapper.InvokeSshProcess(address, "echo \'\'", cancellationToken);
            return !result.StdErr.Any();
        }

        public async Task<bool> RemoteProgramExists(IPAddress address, string name, CancellationToken cancellationToken = default)
        {
            var result = await _sshWrapper.InvokeSshProcess(address, "which " + name, cancellationToken);
            return !result.StdErr.Any();
        }
    }
}