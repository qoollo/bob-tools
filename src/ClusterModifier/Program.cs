using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClusterModifier
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CliHelper.RunWithParsed<ClusterExpandArguments>(args, ExpandCluster);
        }

        private static async Task ExpandCluster(ClusterExpandArguments arguments, IServiceCollection services,
            CancellationToken cancellationToken)
        {
            services.AddTransient<ClusterExpander>();
            var provider = services.BuildServiceProvider();

            var expander = provider.GetRequiredService<ClusterExpander>();
            await expander.ExpandCluster(arguments);
        }
    }
}