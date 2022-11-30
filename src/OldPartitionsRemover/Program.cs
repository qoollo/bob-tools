using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.Entities;
using BobToolsCli;
using BobToolsCli.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OldPartitionsRemover.Infrastructure;

namespace OldPartitionsRemover
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            await CliHelper.RunWithParsed<ByDateRemoving.Arguments, BySpaceRemoving.Arguments>(args,
                RemovePartitionsByDate, RemovePartitionsBySpace);
        }

        private static async Task RemovePartitionsByDate(ByDateRemoving.Arguments args, IServiceCollection services, CancellationToken cancellationToken)
            => await RemovePartitions<ByDateRemoving.Remover>(services, r => r.RemoveOldPartitions(cancellationToken));

        private static async Task RemovePartitionsBySpace(BySpaceRemoving.Arguments args, IServiceCollection services, CancellationToken cancellationToken)
            => await RemovePartitions<BySpaceRemoving.Remover>(services, r => r.RemovePartitionsBySpace(cancellationToken));

        private static async Task RemovePartitions<TRem>(IServiceCollection services, Func<TRem, Task<Entities.Result<int>>> remove)
            where TRem : class
        {
            services.AddTransient<TRem>();
            services.AddTransient<ResultsCombiner>();
            using var provider = services.BuildServiceProvider();
            var remover = provider.GetRequiredService<TRem>();

            var removeResult = await remove(remover);
            if (removeResult.IsOk(out var removed, out var error))
            {
                Console.WriteLine($"Removed {removed} partitions");
            }
            else
            {
                Console.WriteLine($"Error: {error}");
            }
        }
    }
}
