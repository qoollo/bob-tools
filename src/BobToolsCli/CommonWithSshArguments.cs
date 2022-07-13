using CommandLine;
using RemoteFileCopy.Ssh;

namespace BobToolsCli
{
    public class CommonWithSshArguments : CommonArguments
    {
        [Option("ssh-cmd", HelpText = "Ssh cmd.", Default = "ssh")]
        public string SshCmd { get; set; }

        [Option("ssh-port", HelpText = "Ssh port.", Default = 22)]
        public int SshPort { get; set; }

        [Option("ssh-user", HelpText = "Ssh username.", Default = "bobd")]
        public string SshUser { get; set; }

        [Option("ssh-key-path", HelpText = "Path to ssh key.", Default = "~/.ssh/id_rsa")]
        public string SshKeyPath { get; set; }

        public SshConfiguration SshConfiguration => new(SshCmd, SshPort, SshUser, SshKeyPath);
    }
}