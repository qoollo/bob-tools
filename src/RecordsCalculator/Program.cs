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
        private static bool infoEnabled = true;
        private static bool errorEnabled = true;
        private static Configuration configuration;

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

            configuration = args.Any(s => s.EndsWith(".json"))
                ? Configuration.FromJsonFile(args.First(s => s.EndsWith(".json")))
                : Configuration.FromCommandLineArguments(args);
            await CountRecords();
        }

        private static async Task CountRecords()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddConsole());
            services.AddTransient<ClusterRecordsCounter>();
            var prov = services.BuildServiceProvider();
            var crc = prov.GetRequiredService<ClusterRecordsCounter>();
            foreach (var node in configuration.Nodes)
            {
                try
                {
                    var (max, total) = await crc.CountRecordsInCluster(node.Address);
                    LogInfo($"Parsed cluster info from node {node.Address}");
                    Console.WriteLine($"Total records count: {max}");
                    Console.WriteLine($"Total records count with replicas: {total}");
                    return;
                }
                catch (Exception e)
                {
                    LogError($"Failed to parse node from address {node.Address}: {e.Message}");
                }
            }
        }


        private static void LogError(string text)
        {
            if (errorEnabled)
                Console.WriteLine($"ERROR: {text}");
        }

        private static void LogInfo(string text)
        {
            if (infoEnabled)
                Console.WriteLine($"INFO: {text}");
        }

        private class Configuration
        {
            private Configuration(List<string> nodes)
            {
                if (nodes is null || nodes.Count == 0)
                {
                    LogError("Addresses not found");
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
