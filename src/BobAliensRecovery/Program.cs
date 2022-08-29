using System;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery;
using BobApi.BobEntities;
using BobToolsCli;
using BobToolsCli.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using RemoteFileCopy.Extensions;

namespace BobAliensRecovery
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await CliHelper.RunWithParsed<ProgramArguments>(args, RecoverAliens);
        }

        private static async Task RecoverAliens(ProgramArguments arguments, IServiceCollection services, CancellationToken cancellationToken)
        {
            var provider = CreateServiceProvider(services, arguments);
            var recoverer = provider.GetRequiredService<AliensRecoverer>();

            var cluster = await GetClusterConfiguration(arguments!, cancellationToken);

            await recoverer.RecoverAliens(cluster, arguments.GetBobApiClientProvider(), arguments.AliensRecoveryOptions,
                cancellationToken);
        }

        private static async Task<ClusterConfiguration> GetClusterConfiguration(
            ProgramArguments arguments, CancellationToken cancellationToken)
        {
            var result = await arguments.FindClusterConfiguration(cancellationToken);
            if (result.IsOk(out var config, out var err))
                return config;
            else
                throw new ConfigurationException(err);
        }

        private static IServiceProvider CreateServiceProvider(IServiceCollection services, ProgramArguments args)
        {
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
