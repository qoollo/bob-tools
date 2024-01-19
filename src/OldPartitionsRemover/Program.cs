using System;
using System.Threading;
using System.Threading.Tasks;
using BobToolsCli;
using Microsoft.Extensions.DependencyInjection;
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
            services.AddTransient<RemovablePartitionsFinder>();
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
