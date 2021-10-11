using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace RemoteFileCopy.Ssh.Entities
{
    public class SshResult
    {
        public SshResult(IPAddress address, string[] stdOut, string[] stdErr)
        {
            Address = address;
            StdOut = stdOut;
            StdErr = stdErr;
        }

        public IPAddress Address { get; }
        public bool IsError => StdErr.Any(s => !string.IsNullOrWhiteSpace(s));
        public IEnumerable<string> StdOut { get; }
        public IEnumerable<string> StdErr { get; }
    }
}