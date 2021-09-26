using System.Collections.Generic;
using System.Net;

namespace RemoteFileCopy.Ssh.Entities
{
    public class SshResult
    {
        public SshResult(IPAddress address, IEnumerable<string> stdOut, IEnumerable<string> stdErr)
        {
            Address = address;
            StdOut = stdOut;
            StdErr = stdErr;
        }

        public IPAddress Address { get; }
        public IEnumerable<string> StdOut { get; }
        public IEnumerable<string> StdErr { get; }
    }
}