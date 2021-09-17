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
        public string ClusterConfigPath { get; set; } = DefaultClusterConfigPath;

        [Option('v', HelpText = "Verbosity level, 0 to 3.", Default = 0)]
        public int VerbosityLevel { get; set; }

        [Option("api-port", HelpText = "Override default api port for the node. E.g. node1:80,node2:8000.", Separator = ',')]
        public IEnumerable<string> ApiPortOverrides { get; set; } = Enumerable.Empty<string>();

        [Option("ssh-cmd", HelpText = "Ssh cmd.", Default = "ssh")]
        public string SshCmd { get; set; } = "ssh";

        [Option("ssh-port", HelpText = "Ssh port.", Default = 22)]
        public int SshPort { get; set; } = 22;

        [Option("ssh-user", HelpText = "Ssh username.", Default = "bobd")]
        public string SshUser { get; set; } = "bobd";

        [Option("ssh-key-path", HelpText = "Path to ssh key.", Default = "~/.ssh/id_rsa")]
        public string SshKeyPath { get; set; } = "~/.ssh/id_rsa";

        public LoggerOptions LoggerOptions => new(VerbosityLevel);

        public ClusterOptions ClusterOptions
            => new(ApiPortOverrides.Select(s => (s.Split(NamePortSeparator)[0], int.Parse(s.Split(NamePortSeparator)[1]))));

        public SshConfiguration SshConfiguration => new(SshCmd, SshPort, SshUser, SshKeyPath);
    }
}