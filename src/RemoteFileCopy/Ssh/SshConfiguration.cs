namespace RemoteFileCopy.Ssh
{
    public class SshConfiguration
    {
        public SshConfiguration(
            string cmd,
            int port,
            string username,
            string pathToKey)
        {
            Cmd = cmd;
            Port = port;
            Username = username;
            PathToKey = pathToKey;
        }

        public string Cmd { get; }
        public int Port { get; }
        public string Username { get; }
        public string PathToKey { get; }
    }
}