using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;

namespace BobAliensRecovery
{
    class ProgramArguments
    {
        private const char NamePortSeparator = ':';

        private static readonly string s_defaultClusterConfigPath;
        static ProgramArguments()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                s_defaultClusterConfigPath = Path.Combine(
                    Path.DirectorySeparatorChar.ToString(),
                    "etc",
                    "bob",
                    "cluster.yaml");
            }
            else
            {
                s_defaultClusterConfigPath = "cluster.yaml";
            }
        }

        [Option("cluster-config", HelpText = "Cluster config of bob instance")]
        public string ClusterConfigPath { get; set; } = s_defaultClusterConfigPath;

        [Option('v', HelpText = "Verbosity level, 0 to 3", Default = 0)]
        public int VerbosityLevel { get; set; }

        [Option("api-port", HelpText = "Override default api port for the node. E.g. node1:80,node2:8000", Separator = ',')]
        public IEnumerable<string> ApiPortOverrides { get; set; } = Enumerable.Empty<string>();

        public LoggerOptions LoggerOptions
            => new(VerbosityLevel);

        public ClusterOptions ClusterOptions
            => new(ApiPortOverrides.Select(s => (s.Split(NamePortSeparator)[0], int.Parse(s.Split(NamePortSeparator)[1]))));
    }
}