using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobApi.Helpers;
using CommandLine;
using YamlDotNet.Serialization;

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

        public async Task<YamlReadingResult<ClusterConfiguration>> FindClusterConfiguration(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(ClusterConfigPath))
                return YamlReadingResult<ClusterConfiguration>.Error("Configuration file not found");

            var configContent = await File.ReadAllTextAsync(ClusterConfigPath, cancellationToken: cancellationToken);

            try
            {
                var config = new Deserializer().Deserialize<ClusterConfiguration>(configContent);
                return YamlReadingResult<ClusterConfiguration>.Ok(config);
            }
            catch (Exception e)
            {
                return YamlReadingResult<ClusterConfiguration>.Error(e.Message);
            }
        }
    }
}