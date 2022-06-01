using BobToolsCli;
using CommandLine;

namespace ClusterModifier
{
    [Verb("cluster-expand", HelpText = "Expand cluster from old config to current config")]
    public class ClusterExpandArguments : CommonArguments
    {
        [Option("old-config", Required = true, HelpText = "Path to old config")]
        public string OldConfigPath { get; set; }

        [Option("dry-run", Required = false, HelpText = "Do not copy anything")]
        public bool DryRun { get; set; } = false;

        [Option("remove-source", Required = false, HelpText = "Remove source files after copy")]
        public bool RemoveSourceFiles { get; set; } = false;

        [Option("dsa", Required = true, HelpText = "Path to disk status analyzer")]
        public string DiskStatusAnalyzer { get; set; }
    }
}