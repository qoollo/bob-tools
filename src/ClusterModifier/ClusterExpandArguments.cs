using BobToolsCli;
using CommandLine;

namespace ClusterModifier
{
    [Verb("cluster-expand", HelpText = "Expand cluster from old config to current config")]
    public class ClusterExpandArguments : CommonWithSshArguments
    {
        [Option("old-config", Required = true, HelpText = "Path to old config")]
        public string OldConfigPath { get; set; }

        [Option("dry-run", Required = false, HelpText = "Do not copy anything")]
        public bool DryRun { get; set; } = false;

        [Option("remove-unused-replicas", Required = false, HelpText = "Remove files in unused replicas")]
        public bool RemoveUnusedReplicas { get; set; } = false;
    }
}