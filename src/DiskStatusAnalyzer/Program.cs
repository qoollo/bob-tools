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
using Serilog;
using Microsoft.Extensions.Configuration;
using DiskStatusAnalyzer.ReplicaRestoring;
using CommandLine;
using Logger = Microsoft.Extensions.Logging.ILogger<DiskStatusAnalyzer.ProgramStub>;

namespace DiskStatusAnalyzer
{
    static class Program
    {
        private static readonly Deserializer Deserializer = new Deserializer();
        private static readonly IServiceProvider serviceProvider = CreateServiceProvider();
        private static IConfigurationRoot configuration;
        private static Logger logger;

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            AddConfiguration(services);
            AddSerilog(services);
            services.AddTransient<NodesCreator>();
            services.AddTransient<AlienCopier>();
            services.AddTransient<NodeStructureCreator>();
            services.AddTransient<RsyncWrapper>();
            services.AddTransient<ReplicaCopier>();
            var result = services.BuildServiceProvider();
            logger = result.GetRequiredService<Logger>();
            return result;
        }

        static void AddConfiguration(IServiceCollection services)
        {
            configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .Build();
            services.AddSingleton<IConfigurationRoot>(configuration);
        }

        static void AddSerilog(IServiceCollection services)
        {
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            services.AddLogging(b => b.AddSerilog(logger));
        }

        static async Task Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<CopyAliensOptions, CopyDiskOptions>(args);
            var copyAliens = parsed.WithParsedAsync<CopyAliensOptions>(CopyAliens);
            var copyDisk = parsed.WithParsedAsync<CopyDiskOptions>(CopyDisk);
            await Task.WhenAll(copyAliens, copyDisk);
        }

        private static async Task CopyAliens(CopyAliensOptions options)
        {
            var config = await FindConfig(options);
            if (config == null)
                return;

            var nodes = await FindNodes(config);
            if (nodes == null)
                return;

            var alienCopier = serviceProvider.GetRequiredService<AlienCopier>();
            await alienCopier.CopyAlienInformation(nodes, config);
        }

        private static async Task CopyDisk(CopyDiskOptions options)
        {
            var nodes = await FindNodes(options);
            if (nodes == null)
                return;

            var srcNode = FindSingleNode(nodes, options.SourceNodeName);
            var destNode = FindSingleNode(nodes, options.DestNodeName);

            var replicaCopier = serviceProvider.GetRequiredService<ReplicaCopier>();
            if (srcNode == null || destNode == null || !await replicaCopier.Copy(srcNode, options.DiskName, destNode, options.DiskName))
                logger.LogError("Copy failed");
        }

        private static async Task<List<NodeWithDirs>> FindNodes(CommonOptions options)
        {
            var config = await FindConfig(options);
            if (config == null)
                return null;
            return await FindNodes(config);
        }

        private static async Task<List<NodeWithDirs>> FindNodes(Configuration config)
        {
            var nodesCreator = serviceProvider.GetRequiredService<NodesCreator>();
            var nodes = await nodesCreator.CreateNodeStructures(config);

            return nodes;
        }

        private static async Task<Configuration> FindConfig(CommonOptions options)
        {
            if (!File.Exists(options.ConfigFilename))
            {
                logger.LogError($"Config file {options.ConfigFilename} not found");
                return null;
            }
            return Deserializer.Deserialize<Configuration>(await File.ReadAllTextAsync(options.ConfigFilename));
        }

        private static NodeWithDirs FindSingleNode(IEnumerable<NodeWithDirs> nodes, string name)
        {
            var nodesWithName = nodes.Where(n => NamesEqual(n?.Name, name));
            if (nodesWithName.Count() == 1)
                return nodesWithName.Single();
            logger.LogError($"Node with name {name} not found (known nodes: {string.Join(", ", nodes.Select(n => n.Name))})");
            return null;
        }

        private static bool NamesEqual(string x, string y)
        {
            return x?.Equals(y, StringComparison.Ordinal) == true;
        }

        public class CommonOptions
        {
            [Option('c', "config", Required = false, Default = "config.yaml", HelpText = "Configuration file")]
            public string ConfigFilename { get; set; }
        }

        [Verb("copy-aliens", isDefault: true, HelpText = "Copy aliens from known nodes")]
        public class CopyAliensOptions : CommonOptions
        {

        }

        [Verb("copy-disk", HelpText = "Copy disk content from one node to another")]
        public class CopyDiskOptions : CommonOptions
        {
            [Option('n', "name", Required = true, HelpText = "Disk name")]
            public string DiskName { get; set; }

            [Option('s', "src", Required = true, HelpText = "Source node name")]
            public string SourceNodeName { get; set; }

            [Option('d', "dst", Required = true, HelpText = "Destination node name")]
            public string DestNodeName { get; set; }
        }
    }

    class ProgramStub { }
}
