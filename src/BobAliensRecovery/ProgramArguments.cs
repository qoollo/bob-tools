using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using RemoteFileCopy.Ssh;

namespace BobAliensRecovery
{
    class ProgramArguments
    {
        private const char NamePortSeparator = ':';
        private const string DefaultClusterConfigPath = "/etc/bob/cluster.yaml";


        [Option("cluster-config", HelpText = "Cluster config of bob instance.", Default = DefaultClusterConfigPath)]
        public string? ClusterConfigPath { get; set; }

        [Option('v', HelpText = "Verbosity level, 0 to 3.", Default = 0)]
        public int VerbosityLevel { get; set; }

        [Option("api-port", HelpText = "Override default api port for the node. E.g. node1:80,node2:8000. Wildcard char (*) can be used to set port for all nodes.", Separator = ',')]
        public IEnumerable<string> ApiPortOverrides { get; set; } = Enumerable.Empty<string>();

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

        [Option("continue-on-error", HelpText = "Continue copy on cluster state errors", Default = false)]
        public bool ContinueOnError { get; set; }

        [Option("restart-nodes", HelpText = "Restart nodes after aliens have been copied", Default = false)]
        public bool RestartNodes { get; set; }

        public LoggerOptions LoggerOptions => new(VerbosityLevel);

        public ClusterOptions ClusterOptions
            => new(ApiPortOverrides.Select(s => (s.Split(NamePortSeparator)[0], int.Parse(s.Split(NamePortSeparator)[1]))));

        public SshConfiguration SshConfiguration => new(SshCmd!, SshPort, SshUser!, SshKeyPath!);

        public AliensRecoveryOptions AliensRecoveryOptions => new(RemoveCopied, ContinueOnError, RestartNodes);
    }
}