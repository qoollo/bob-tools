using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery;
using BobAliensRecovery.Exceptions;
using BobApi.BobEntities;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Exceptions;
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
            catch (MissingDependencyException e)
            {
                Console.WriteLine($"Missing dependency: {e.Message}");
            }
            catch (ClusterStateException e)
            {
                Console.WriteLine($"Cluster state is invalid: {e.Message}");
            }
            catch (ConfigurationException e)
            {
                Console.WriteLine($"Configuration is invalid: {e.Message}");
            }
            catch (OperationException e)
            {
                Console.WriteLine($"Execution failed: {e.Message}");
            }
            catch (CommandLineFailureException e)
            {
                Console.WriteLine($"Command line failure: {e.Message}");
            }
        }

        private static async Task RecoverAliens(ProgramArguments arguments, CancellationToken cancellationToken)
        {
            var provider = CreateServiceProvider(arguments);
            var logger = provider.GetRequiredService<ILogger<Program>>();
            var recoverer = provider.GetRequiredService<AliensRecoverer>();

            var cluster = await GetClusterConfiguration(logger, arguments.ClusterConfigPath!, cancellationToken);

            await recoverer.RecoverAliens(cluster, arguments.ClusterOptions, arguments.AliensRecoveryOptions,
                cancellationToken);
        }

        private static async Task<ClusterConfiguration> GetClusterConfiguration(
            ILogger<Program> logger, string path, CancellationToken cancellationToken)
        {
            logger.LogDebug("Received cluster config path: {path}", path);
            if (!File.Exists(path))
                throw new ConfigurationException($"Cluster configuration file not found in {path}");

            var configContent = await File.ReadAllTextAsync(path, cancellationToken: cancellationToken);
            var cluster = new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
                .Deserialize<ClusterConfiguration>(configContent);
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
