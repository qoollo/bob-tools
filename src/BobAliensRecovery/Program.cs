using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery;
using BobApi.BobEntities;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entites;
using RemoteFileCopy.Extensions;
using YamlDotNet.Serialization;

namespace BobAliensRecovery
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<ProgramArguments>(args);
            _ = await parsed.WithParsedAsync(RecoverAliens);
        }

        private static async Task RecoverAliens(ProgramArguments arguments)
        {
            var cancellationToken = new CancellationTokenSource().Token;
            var provider = CreateServiceProvider(arguments.LoggerOptions);
            var logger = provider.GetRequiredService<ILogger<Program>>();

            try
            {
                var cluster = await GetClusterConfiguration(logger, arguments.ClusterConfigPath, cancellationToken);

                await provider.GetRequiredService<AliensRecoverer>().RecoverAliens(cluster, cancellationToken);
            }
            catch (ArgumentException e)
            {
                logger.LogError(e.Message);
            }
        }

        private static async Task<ClusterConfiguration> GetClusterConfiguration(
            ILogger<Program> logger, string path, CancellationToken cancellationToken)
        {
            logger.LogDebug("Received cluster config path: {path}", path);
            if (!File.Exists(path))
                throw new ArgumentException($"Cluster configuration file not found in {path}");

            var configContent = await File.ReadAllTextAsync(path, cancellationToken: cancellationToken);
            var cluster = new Deserializer().Deserialize<ClusterConfiguration>(configContent);
            return cluster;
        }

        private static IServiceProvider CreateServiceProvider(LoggerOptions loggerOptions)
        {
            var services = new ServiceCollection();

            services.AddLogging(b => b.AddConsole().SetMinimumLevel(loggerOptions.MinLevel));

            services.AddRemoteFileCopy();

            services.AddScoped<AliensRecoverer>();

            return services.BuildServiceProvider();
        }
    }
}
