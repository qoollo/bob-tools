using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.ConfigurationFinding;
using BobToolsCli.ConfigurationReading;
using BobToolsCli.Helpers;
using CommandLine;
using Microsoft.Extensions.Logging;

namespace BobToolsCli
{
    public class CommonArguments : IConfigurationFinder
    {
        private const string DefaultClusterConfigPath = "/etc/bob/cluster.yaml";

        [Option("cluster-config", HelpText = "Cluster config of bob instance.", Default = DefaultClusterConfigPath)]
        public string ClusterConfigPath { get; set; }

        [Option('v', HelpText = "Verbosity level, 0 to 3.", Default = 0)]
        public int VerbosityLevel { get; set; }

        [Option("api-port", HelpText = "Override default api port for the node. E.g. node1:80,node2:8000. Wildcard char (*) can be used to set port for all nodes.", Separator = ',')]
        public IEnumerable<string> ApiPortOverrides { get; set; } = Enumerable.Empty<string>();

        [Option("credentials", HelpText = "Override credentials for the nodes. Form is dest:user=password, separated by ','. E.g. node1:user1=pass1,node2:user2=pass2.", Separator = ',')]
        public IEnumerable<string> Credentials { get; set; } = Enumerable.Empty<string>();

        [Option("user", HelpText = "User for node authentication")]
        public string Username { get; set; }

        [Option("password", HelpText = "Password for node authentication")]
        public string Password { get; set; }

        [Option("continue-on-error", HelpText = "Continue copy on cluster state errors", Default = false)]
        public bool ContinueOnError { get; set; }

        [Option("bootstrap-node", HelpText = "Load config from node instead of file. Node is specified by host and port, e.g. 127.0.0.1:8000, localhost:8000")]
        public string BootstrapNode { get; set; }

        [Option("file-log", HelpText = "Path to file to log")]
        public string FileLogPath { get; set; }

        public async Task<ConfigurationReadingResult<ClusterConfiguration>> FindClusterConfiguration(bool skipUnavailableNodes = false, CancellationToken cancellationToken = default)
        {
            if (BootstrapNode != null)
            {
                if (TryParseHostPort(BootstrapNode, out var host, out var port))
                    return await new NodeClusterConfigurationFetcher(GetBobApiClientProvider()).GetConfigurationFromNode(host, port, skipUnavailableNodes, cancellationToken);
                else
                    return ConfigurationReadingResult<ClusterConfiguration>.Error($"Failed to parse bootstrap node address from \"{BootstrapNode}\"");
            }

            return await GetClusterConfigurationFromFile(ClusterConfigPath, cancellationToken);
        }

        public async Task<ConfigurationReadingResult<ClusterConfiguration>> GetClusterConfigurationFromFile(string path,
            CancellationToken cancellationToken = default)
        {
            return await new ClusterConfigurationReader().ReadConfigurationFromFile(path, cancellationToken);
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

        public BobApiClientProvider GetBobApiClientProvider()
        {
            return new BobApiClientProvider(ApiPortOverrides, Credentials, Username, Password);
        }

        private static bool TryParseHostPort(string s, out string host, out int port)
        {
            host = null;
            port = 0;
            if (s.Contains(':'))
            {
                var split = s.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (split.Length == 2 && int.TryParse(split[1], out port))
                {
                    host = split[0];
                    return true;
                }
            }

            return false;
        }
    }
}
