using System;
using System.Threading;
using System.Threading.Tasks;
using BobToolsCli;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RecordsCalculator
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            await CliHelper.RunWithParsed<ProgramArguments>(args, CountRecords);
        }

        private static async Task CountRecords(ProgramArguments arguments, CancellationToken cancellationToken)
        {
            var provider = new ServiceCollection()
                .AddLogging(b => b.AddConsole().SetMinimumLevel(arguments.GetMinLogLevel()))
                .AddTransient<ClusterRecordsCounter>()
                .AddSingleton(arguments)
                .BuildServiceProvider();
            var counter = provider.GetRequiredService<ClusterRecordsCounter>();
            var configResult = await arguments.FindClusterConfiguration(cancellationToken);
            if (configResult.IsOk(out var configuration, out var error))
            {
                var result = await counter.CountRecordsInCluster(configuration, cancellationToken);
                Console.WriteLine($"Total records count: {result.Unique}");
                Console.WriteLine($"Total records count with replicas: {result.WithReplicas}");
            }
            else
            {
                Console.WriteLine($"Error reading config: {error}");
            }
        }
    }
}
