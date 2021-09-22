using System.Collections.Generic;

namespace RemoteFileCopy.Ssh.Entities
{
    public class SshResult
    {
        public SshResult(IEnumerable<string> stdOut, IEnumerable<string> stdErr)
        {
            StdOut = stdOut;
            StdErr = stdErr;
        }

        public IEnumerable<string> StdOut { get; }
        public IEnumerable<string> StdErr { get; }
    }
}