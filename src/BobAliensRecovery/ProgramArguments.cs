using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobApi.Helpers;
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

        public LoggerOptions LoggerOptions => new(VerbosityLevel);

        public ClusterOptions ClusterOptions => new(ApiPortOverrides);

        public SshConfiguration SshConfiguration => new(SshCmd!, SshPort, SshUser!, SshKeyPath!);

        public AliensRecoveryOptions AliensRecoveryOptions => new(RemoveCopied, ContinueOnError);
    }
}