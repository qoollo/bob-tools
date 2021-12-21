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

namespace OldPartitionsRemover
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            await CliHelper.RunWithParsed<ByDateRemoving.Arguments>(args, RemoveOldPartitions);
        }

        private static async Task RemoveOldPartitions(ByDateRemoving.Arguments args, IServiceCollection services, CancellationToken cancellationToken)
        {
            var provider = services.BuildServiceProvider();
            var remover = provider.GetRequiredService<ByDateRemoving.Remover>();
            var logger = provider.GetRequiredService<ILogger<Program>>();

            var removeResult = await remover.RemoveOldPartitions(cancellationToken);
            if (removeResult.IsOk(out var success, out var error))
            {
                if (!success)
                    logger.LogError("Failed to delete partitions due to internal error");
            }
            else
            {
                logger.LogError("Failed to delete partitions: {Error}", error);
            }
        }
    }
}
