using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
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

        [Option("dry-run", Required = false, HelpText = "Special testing run with no interactions with cluster")]
        public bool TestRun { get; set; } = false;

        [Option("remove-unused-replicas", Required = false, HelpText = "Remove files in unused replicas")]
        public bool RemoveUnusedReplicas { get; set; } = false;

        [Option("force-remove-unused-replicas-without-copies", Required = false, HelpText = "Remove replicas without copies even if error occured")]
        public bool ForceRemoveUncopiedUnusedReplicas { get; set; } = false;

        [Option("bob-root-dir", HelpText = "Root dirs for bob nodes. E.g. node1:bob,node2:rootdir. Wildcard char (*) can be used to set root dir for all nodes.", Separator = ',')]
        public IEnumerable<string> BobRootDirOverrides { get; set; } = Enumerable.Empty<string>();

        [Option("copy-parallel-degree", HelpText = "Number of simultaneous copy processes", Default = 1)]
        public int CopyParallelDegree { get; set; }

        public async ValueTask<string> GetRootDir(
            ClusterConfiguration.Node node,
            CancellationToken cancellationToken = default
        )
        {
            var rootDir = FindRootDirOverride(node.Name);
            if (rootDir == null)
            {
                var client = GetBobApiClientProvider().GetClient(node);
                var nodeConfigResult = await client.GetNodeConfiguration(cancellationToken);
                if (nodeConfigResult.IsOk(out var conf, out var error))
                    rootDir = conf.RootDir;
                else
                    throw new ClusterStateException(
                        $"Node {node.Name} configuration is unavailable: {error}, "
                            + "and bob-root-dir does not contain enough information"
                    );
            }
            return rootDir;
        }

        private string FindRootDirOverride(string nodeName)
        {
            if (BobRootDirOverrides.Any())
            {
                foreach (var rootDir in BobRootDirOverrides)
                {
                    var split = rootDir.Split(':');
                    if (split.Length != 2)
                        throw new ConfigurationException("Malformed overrides argument");

                    if (split[0] == nodeName || split[0] == "*")
                        return split[1];
                }
            }
            return null;
        }
    }
}
