﻿using System;
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
            try
            {
                await CliHelper.RunWithParsed<ProgramArguments>(args, CountRecords);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        private static async Task CountRecords(ProgramArguments arguments, IServiceCollection services,
            CancellationToken cancellationToken)
        {
            using var provider = services
                .AddTransient<ClusterRecordsCounter>()
                .BuildServiceProvider();

            var counter = provider.GetRequiredService<ClusterRecordsCounter>();
            var configResult = await arguments.FindClusterConfiguration(cancellationToken: cancellationToken);
            if (configResult.IsOk(out var configuration, out var error))
            {
                try
                {
                    var result = await counter.CountRecordsInCluster(configuration, cancellationToken);
                    Console.WriteLine($"Total records count: {result.Unique}");
                    Console.WriteLine($"Total records count with replicas: {result.WithReplicas}");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Error reading counts");
                }
            }
            else
            {
                Console.WriteLine($"Error reading config: {error}");
            }
        }
    }
}
