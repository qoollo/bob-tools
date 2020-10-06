using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.NodeStructureCreation;
using DiskStatusAnalyzer.Rsync;
using DiskStatusAnalyzer.Rsync.Entities;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using Microsoft.Extensions.Logging;

namespace DiskStatusAnalyzer
{
    static class Program
    {
        private static readonly Deserializer Deserializer = new Deserializer();

        private static readonly IServiceProvider serviceProvider = CreateServiceProvider();

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddTransient<NodesCreator>();
            services.AddTransient<AlienCopier>();
            services.AddTransient<NodeStructureCreator>();
            services.AddTransient<RsyncWrapper>();
            return services.BuildServiceProvider();
        }

        static async Task Main(string[] args)
        {
            var nodesCreator = serviceProvider.GetRequiredService<NodesCreator>();
            var alienCopier = serviceProvider.GetRequiredService<AlienCopier>();

            var config = await GetConfiguration(args);

            var nodes = await nodesCreator.CreateNodeStructures(config);

            await alienCopier.CopyAlienInformation(nodes, config);
        }

        private static async Task<Configuration> GetConfiguration(string[] args)
        {
            var configFilename = args.Length > 0 ? args[0] : "config.yaml";
            var config = Deserializer.Deserialize<Configuration>(await File.ReadAllTextAsync(configFilename));
            return config;
        }


        internal static void LogError(string s)
        {
            Console.WriteLine($"ERROR: {s}");
        }

        internal static void LogInfo(string s)
        {
            Console.WriteLine($"INFO: {s}");
        }
    }
}
