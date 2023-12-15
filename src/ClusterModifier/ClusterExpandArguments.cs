using System.Collections.Generic;
using System.Linq;
using BobToolsCli;
using BobToolsCli.Exceptions;
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

        [Option("force-remove-unused-replicas-without-copies", Required = false, HelpText = "Remove replicas without copies even if error occured")]
        public bool ForceRemoveUncopiedUnusedReplicas { get; set; } = false;

        [Option("bob-root-dir", HelpText = "Root dirs for bob nodes. E.g. node1:bob,node2:rootdir. Wildcard char (*) can be used to set root dir for all nodes.", Separator = ',')]
        public IEnumerable<string> BobRootDirOverrides { get; set; } = Enumerable.Empty<string>();

        [Option("copy-parallel-degree", HelpText = "Number of simultaneous copy processes", Default = 1)]
        public int CopyParallelDegree { get; set; }

        public string FindRootDir(string node)
        {
            if (BobRootDirOverrides.Any())
            {
                foreach (var rootDir in BobRootDirOverrides)
                {
                    var split = rootDir.Split(':');
                    if (split.Length != 2)
                        throw new ConfigurationException("Malformed overrides argument");

                    if (split[0] == node || split[0] == "*")
                        return split[1];
                }
            }
            return null;
        }
    }
}
