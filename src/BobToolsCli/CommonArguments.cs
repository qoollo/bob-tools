using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobApi.Helpers;
using CommandLine;

namespace BobToolsCli
{
    public class CommonArguments
    {
        private const string DefaultClusterConfigPath = "/etc/bob/cluster.yaml";

        [Option("cluster-config", HelpText = "Cluster config of bob instance.", Default = DefaultClusterConfigPath)]
        public string ClusterConfigPath { get; }

        [Option('v', HelpText = "Verbosity level, 0 to 3.", Default = 0)]
        public int VerbosityLevel { get; }

        [Option("api-port", HelpText = ApiPortOverridesParser.HelpText, Separator = ',')]
        public IEnumerable<string> ApiPortOverrides { get; set; } = Enumerable.Empty<string>();

        [Option("continue-on-error", HelpText = "Continue copy on cluster state errors", Default = false)]
        public bool ContinueOnError { get; set; }

        public async Task<ClusterConfiguration> FindClusterConfiguration(CancellationToken cancellationToken = default)
        {
            return await BobYamlClusterConfigParser.ParseYaml(ClusterConfigPath, cancellationToken);
        }
    }
}