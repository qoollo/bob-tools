using BobToolsCli;
using CommandLine;
using RemoteFileCopy.Ssh;

namespace BobAliensRecovery
{
    class ProgramArguments : CommonArguments
    {
        [Option("ssh-cmd", HelpText = "Ssh cmd.", Default = "ssh")]
        public string? SshCmd { get; set; }

        [Option("ssh-port", HelpText = "Ssh port.", Default = 22)]
        public int SshPort { get; set; }

        [Option("ssh-user", HelpText = "Ssh username.", Default = "bobd")]
        public string? SshUser { get; set; }

        [Option("ssh-key-path", HelpText = "Path to ssh key.", Default = "~/.ssh/id_rsa")]
        public string? SshKeyPath { get; set; }

        [Option("remove-copied", HelpText = "Remove copied blobs", Default = false)]
        public bool RemoveCopied { get; set; }

        [Option("restart-nodes", HelpText = "Restart nodes after aliens have been copied", Default = false)]
        public bool RestartNodes { get; set; }

        [Option("copy-parallel-degree", HelpText = "Number of simultaneous copy processes", Default = 1)]
        public int CopyParallelDegree { get; set; }

        public SshConfiguration SshConfiguration => new(SshCmd!, SshPort, SshUser!, SshKeyPath!);

        public AliensRecoveryOptions AliensRecoveryOptions
            => new(RemoveCopied, ContinueOnError, RestartNodes, CopyParallelDegree);
    }
}
