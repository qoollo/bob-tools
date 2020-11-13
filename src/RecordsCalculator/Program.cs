using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BobApi;
using BobApi.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Newtonsoft.Json;

namespace RecordsCalculator
{
    internal class Program
    {
        private static readonly string noInfoFlag = "-noinfo";
        private static readonly string noErrorFlag = "-noerror";
        private static readonly string wholeClusterFlag = "-cluster";
        private static bool infoEnabled = true;
        private static bool errorEnabled = true;
        private static bool wholeCluster = false;
        private static ServiceProvider provider;
        private static Configuration configuration;
        private static ILogger logger;

        private static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(
                    $"Usage: \"{Assembly.GetExecutingAssembly().GetName().Name} filename.json\" with addresses or" +
                    $"\"{Assembly.GetExecutingAssembly().GetName().Name} [-a address](multiple times)\"," +
                    $" also available flags are {noInfoFlag} and {noErrorFlag}");
                return;
            }

            if (args.Contains(noInfoFlag))
                infoEnabled = false;
            if (args.Contains(noErrorFlag))
                errorEnabled = false;
            wholeCluster = args.Contains(wholeClusterFlag);

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddConsole());
            services.AddTransient<ClusterRecordsCounter>();
            provider = services.BuildServiceProvider();
            logger = provider.GetRequiredService<ILogger<Program>>();
            configuration = args.Any(s => s.EndsWith(".json"))
                ? Configuration.FromJsonFile(args.First(s => s.EndsWith(".json")))
                : Configuration.FromCommandLineArguments(args);

            await CountRecords();
        }

        private static async Task CountRecords()
        {
            var crc = provider.GetRequiredService<ClusterRecordsCounter>();
            long max = 0, total = 0;
            if (wholeCluster)
                (max, total) = await CollectRecordsInWholeCluster(crc);
            else
                (max, total) = await CollectRecordsFromNodes(crc);

            Console.WriteLine($"Total records count: {max}");
            Console.WriteLine($"Total records count with replicas: {total}");
        }

        private static async Task<(long max, long total)> CollectRecordsInWholeCluster(ClusterRecordsCounter crc)
        {
            foreach (var node in configuration.Nodes)
            {
                try
                {
                    return await crc.CountRecordsInCluster(node.Address);
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to parse node from address {node.Address}: {e.Message}");
                }
            }
            return (0, 0);
        }

        private static async Task<(long max, long total)> CollectRecordsFromNodes(ClusterRecordsCounter crc)
        {
            var apiByName = new Dictionary<string, BobApiClient>();
            var vdisks = new List<VDisk>();
            foreach (var node in configuration.Nodes)
            {
                try
                {
                    var api = new BobApiClient(node.Address);
                    var status = await api.GetStatus();
                    if (status == null)
                    {
                        logger.LogError($"Node {node.Address} not available");
                        continue;
                    }
                    apiByName.Add(status?.Name, api);
                    foreach (var vdisk in status?.VDisks)
                    {
                        if (!vdisks.Any(vd => vd.Id == vdisk.Id))
                            vdisks.Add(vdisk);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError($"Error getting info from node {node.Address}, {e.Message}");
                }
            }
            return await crc.CountRecords(apiByName, vdisks);
        }

        private class Configuration
        {
            private Configuration(List<string> nodes)
            {
                if (nodes is null || nodes.Count == 0)
                {
                    logger.LogError("Addresses not found");
                    return;
                }

                Nodes = new List<Node>();
                foreach (var node in nodes)
                {
                    Nodes.Add(new Node(node));
                }
            }

            public List<Node> Nodes { get; }

            public static Configuration FromCommandLineArguments(string[] args)
            {
                var addresses = new List<string>();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-a")
                        addresses.Add(args[i + 1]);
                }

                return new Configuration(addresses);
            }

            public static Configuration FromJsonFile(string filename)
            {
                var obj = JsonConvert.DeserializeAnonymousType(File.ReadAllText(filename), new
                {
                    addresses = new List<string>(),
                });
                return new Configuration(obj.addresses);
            }
        }

        private struct Node
        {
            public Node(string s)
            {
                if (Uri.TryCreate(s, UriKind.Absolute, out var address))
                    Address = address;
                else
                    throw new ArgumentException(nameof(Address));
            }

            public Uri Address { get; }
            public override string ToString()
            {
                return $"{Address}";
            }
        }
    }
}
