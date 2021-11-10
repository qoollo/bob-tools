using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.Helpers;
using CommandLine;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace BobToolsCli
{
    public class CommonArguments
    {
        private const string DefaultClusterConfigPath = "/etc/bob/cluster.yaml";

        [Option("cluster-config", HelpText = "Cluster config of bob instance.", Default = DefaultClusterConfigPath)]
        public string ClusterConfigPath { get; set; }

        [Option('v', HelpText = "Verbosity level, 0 to 3.", Default = 0)]
        public int VerbosityLevel { get; set; }

        [Option("api-port", HelpText = "Override default api port for the node. E.g. node1:80,node2:8000. Wildcard char (*) can be used to set port for all nodes.", Separator = ',')]
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

        public LogLevel GetMinLogLevel()
        {
            return VerbosityLevel switch
            {
                3 => LogLevel.Trace,
                2 => LogLevel.Information,
                1 => LogLevel.Error,
                0 => LogLevel.None,
                _ => throw new ArgumentException("Verbosity must be in range [0; 3]")
            };
        }

        public NodePortStorage GetNodePortStorage()
        {
            return new NodePortStorage(ApiPortOverrides);
        }
    }
}