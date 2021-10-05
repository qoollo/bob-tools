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
using RemoteFileCopy.Extensions;
using YamlDotNet.Serialization;

namespace BobAliensRecovery
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            var parsed = Parser.Default.ParseArguments<ProgramArguments>(args);

            try
            {
                _ = await parsed.WithParsedAsync(a => RecoverAliens(a, cancellationTokenSource.Token));
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Cancelled");
            }
        }

        private static async Task RecoverAliens(ProgramArguments arguments, CancellationToken cancellationToken)
        {
            var provider = CreateServiceProvider(arguments);
            var logger = provider.GetRequiredService<ILogger<Program>>();
            var recoverer = provider.GetRequiredService<AliensRecoverer>();

            try
            {
                var cluster = await GetClusterConfiguration(logger, arguments.ClusterConfigPath!, cancellationToken);

                await recoverer.RecoverAliens(cluster, arguments.ClusterOptions, arguments.AliensRecoveryOptions,
                    cancellationToken);
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
                throw new FileNotFoundException($"Cluster configuration file not found in {path}");

            var configContent = await File.ReadAllTextAsync(path, cancellationToken: cancellationToken);
            var cluster = new Deserializer().Deserialize<ClusterConfiguration>(configContent);
            return cluster;
        }

        private static IServiceProvider CreateServiceProvider(ProgramArguments args)
        {
            var services = new ServiceCollection();

            services.AddLogging(b => b.AddConsole().SetMinimumLevel(args.LoggerOptions.MinLevel));

            services
                .AddScoped<AliensRecoverer>()
                .AddScoped<AlienDirsFinder>()
                .AddScoped<BlobsMover>()
                .AddScoped<NodesRestarter>()
                .AddScoped<PartitionInfoAggregator>()
                .AddScoped<RecoveryTransactionsProducer>()
                .AddScoped<ReplicasFinder>();

            services.AddRemoteFileCopy(args.SshConfiguration);

            return services.BuildServiceProvider();
        }
    }
}
