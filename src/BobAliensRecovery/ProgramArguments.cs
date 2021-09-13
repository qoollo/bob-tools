using System;
using System.IO;
using CommandLine;

namespace BobAliensRecovery
{
    class ProgramArguments
    {
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

        public LoggerOptions LoggerOptions => new(VerbosityLevel);
    }
}