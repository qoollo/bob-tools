using System.Collections.Generic;
using System.Linq;

namespace RemoteFileCopy.Ssh
{
    public class SshConfiguration
    {
        public SshConfiguration(
            string cmd,
            IEnumerable<string> flags,
            int port,
            string username,
            string pathToKey)
        {
            Cmd = cmd;
            Flags = flags.ToArray();
            Port = port;
            Username = username;
            PathToKey = pathToKey;
        }

        public string Cmd { get; }
        public string[] Flags { get; }
        public int Port { get; }
        public string Username { get; }
        public string PathToKey { get; }
    }
}
