using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.NodeStructureCreation;
using DiskStatusAnalyzer.ReplicaRestoring;
using DiskStatusAnalyzer.Rsync;
using DiskStatusAnalyzer.Rsync.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Serilog;
using YamlDotNet.Serialization;
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
            var dirs = new[]
            {
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(System.AppContext.BaseDirectory)
            };
            var providers = dirs.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => new PhysicalFileProvider(d));
            var prov = new CompositeFileProvider(providers);
            configuration = new ConfigurationBuilder()
                .SetFileProvider(prov)
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

        static async Task<int> Main(string[] args)
        {
            return await Parser.Default.ParseArguments<CopyAliensOptions, CopyDiskOptions, CopyDirOptions, RemoveDirOptions>(args)
                .MapResult<CopyAliensOptions, CopyDiskOptions, CopyDirOptions, RemoveDirOptions, Task<int>>(CopyAliens, CopyDisk, CopyDir, RemoveDir, errs => Task.FromResult(1));
        }

        private static async Task<int> CopyAliens(CopyAliensOptions options)
        {
            var config = await FindConfig(options);
            if (config == null)
                return 1;

            var nodes = await FindNodes(config);
            if (nodes == null)
                return 1;

            var alienCopier = serviceProvider.GetRequiredService<AlienCopier>();
            await alienCopier.CopyAlienInformation(nodes, config);

            return 0;
        }

        private static async Task<int> CopyDisk(CopyDiskOptions options)
        {
            try
            {
                var (srcNode, destNode) = await FindSrcDst(options);
                if (srcNode is null || destNode is null)
                    return 1;
                var replicaCopier = serviceProvider.GetRequiredService<ReplicaCopier>();
                if (!await replicaCopier.Copy(srcNode, destNode, options.VDiskId))
                {
                    logger.LogError("Copy failed");
                    return 1;
                }

                return 0;
            }
            catch (Exception e)
            {
                logger.LogError($"Exception during copy: {e.Message}");
                return 1;
            }
        }

        private static async Task<int> CopyDir(CopyDirOptions options)
        {
            try
            {
                var (srcNode, destNode) = await FindSrcDst(options);
                if (srcNode is null || destNode is null)
                    return 1;
                var replicaCopier = serviceProvider.GetRequiredService<ReplicaCopier>();
                if (!await replicaCopier.Copy(srcNode, destNode, options.SourceDir, options.DestDir))
                {
                    logger.LogError("Copy failed");
                    return 1;
                }

                return 0;
            }
            catch (Exception e)
            {
                logger.LogError($"Exception during copy: {e.Message}");
                return 1;
            }
        }

        private static async Task<int> RemoveDir(RemoveDirOptions options)
        {
            try
            {
                var nodes = await FindNodes(options);
                if (nodes == null)
                {
                    logger.LogError("Nodes not found");
                    return 1;
                }
                var node = FindSingleNode(nodes, options.Node);

                var wrapper = serviceProvider.GetRequiredService<RsyncWrapper>();
                if (!await wrapper.RemoveDir(node.ConnectionInfo, options.Dir))
                {
                    logger.LogError("Remove failed");
                    return 1;
                }
                return 0;
            }
            catch (Exception e)
            {
                logger.LogError($"Exception during dir remove: {e.Message}");
                return 1;
            }
        }

        private static async Task<List<NodeWithDirs>> FindNodes(CommonOptions options)
        {
            var config = await FindConfig(options);
            if (config == null)
                return null;
            return await FindNodes(config);
        }

        private static async Task<(NodeWithDirs src, NodeWithDirs dst)> FindSrcDst(CopyBetweenNodesOptions options)
        {
            var nodes = await FindNodes(options);
            if (nodes == null)
                return (null, null);

            var srcNode = FindSingleNode(nodes, options.SourceNodeName);
            var destNode = FindSingleNode(nodes, options.DestNodeName);
            return (srcNode, destNode);
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
            return x?.Equals(y, StringComparison.OrdinalIgnoreCase) == true;
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

        public class CopyBetweenNodesOptions : CommonOptions
        {
            [Option('s', "src", Required = true, HelpText = "Source node name")]
            public string SourceNodeName { get; set; }

            [Option('d', "dst", Required = true, HelpText = "Destination node name")]
            public string DestNodeName { get; set; }
        }

        [Verb("copy-vdisk", HelpText = "Copy vdisk content from one node to another")]
        public class CopyDiskOptions : CopyBetweenNodesOptions
        {
            [Option('v', "vdisk-id", Required = true, HelpText = "VDisk id")]
            public int VDiskId { get; set; }
        }

        [Verb("copy-dir", HelpText = "Copy directory from one node to another")]
        public class CopyDirOptions : CopyBetweenNodesOptions
        {
            [Option("source-dir", Required = true, HelpText = "Directory to copy from on source node")]
            public string SourceDir { get; set; }

            [Option("dest-dir", Required = true, HelpText = "Directory to copy to on destionation node")]
            public string DestDir { get; set; }
        }

        [Verb("remove-dir", HelpText = "Remove directory from node")]
        public class RemoveDirOptions : CommonOptions
        {
            [Option("node", Required = true, HelpText = "Node name")]
            public string Node { get; set; }

            [Option("dir", Required = true, HelpText = "Directory to remove")]
            public string Dir { get; set; }
        }
    }

    class ProgramStub { }
}
