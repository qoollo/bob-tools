using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using BobApi;
using BobApi.Entities;

namespace OldPartitionsRemover
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
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            if (args.Length == 0)
            {
                Console.WriteLine(
                    $"Usage: \"{assemblyName} filename.json\" with addresses, threshold in file, or "
                    + $"\"{assemblyName} [-a address](can be added multiple times) -t threshold\","
                    + $" also available flags are {noInfoFlag} and {noErrorFlag}");
                return;
            }

            if (args.Contains(noInfoFlag))
                infoEnabled = false;
            if (args.Contains(noErrorFlag))
                errorEnabled = false;

            configuration = args.Any(s => s.EndsWith(".json"))
                ? Configuration.FromJsonFile(args.First(s => s.EndsWith(".json")))
                : Configuration.FromCommandLineArguments(args);
            await RemoveOldPartitions();
        }

        private static async Task RemoveOldPartitions()
        {
            if (configuration?.Valid != true)
            {
                LogError("Bad configuration");
                return;
            }

            foreach (var node in configuration.Node)
            {
                using var api = new BobApiClient(node.Address);
                var status = await api.GetStatus();
                if (status == null)
                    continue;
                foreach (var vDisk in status.VDisks)
                {
                    foreach (var replica in vDisk.Replicas)
                    {
                        if (replica.Node == status.Name)
                        {
                            LogInfo($"Processing replica of vdisk {vDisk.Id} on node {node}");
                            var partitions = await api.GetPartitions(vDisk);
                            if (partitions == null)
                                LogError($"Partitions for {vDisk} not found");
                            else
                            {
                                LogInfo($"Found {partitions.Count} partitions on {vDisk}");
                                await DeleteOldPartitions(node.Address, vDisk, partitions);
                            }
                        }
                    }
                }
            }
        }

        private static async Task DeleteOldPartitions(Uri uri, VDisk vDisk, List<string> partitions)
        {
            using var api = new BobApiClient(uri);
            foreach (var partition in partitions)
            {
                var partitionObject = await api.GetPartition(vDisk, partition);
                if (partitionObject is null)
                    LogError($"Failed to get partition {partition} on {vDisk}");
                else if (GetDateTimeFromTimestamp(partitionObject.Timestamp) < configuration.Threshold)
                {
                    await api.DeletePartition(vDisk, partitionObject.Timestamp);
                    LogInfo($"Deleted partition {partition} on {vDisk}");
                }
            }
        }

        private static DateTime GetDateTimeFromTimestamp(long p) => new DateTime(1970, 1, 1).AddSeconds(p);

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
            private static readonly Regex DaysOffsetRegex = new Regex(@"^\-(\d+)d$");

            private Configuration(List<string> nodes, string threshold) : this(nodes)
            {
                if (threshold is null)
                    LogError("Threshold not found");
                if (nodes == null || nodes.Count == 0 || threshold == null) return;

                if (DaysOffsetRegex.IsMatch(threshold))
                    Threshold = DateTime.Now - TimeSpan.FromDays(
                        int.Parse(DaysOffsetRegex.Match(threshold).Groups[1].Value));
                else if (DateTime.TryParse(threshold, out var dateTimeThreshold))
                    Threshold = dateTimeThreshold;
                else
                    LogError("Failed to parse threshold");
            }

            private Configuration(List<string> nodes)
            {
                if (nodes is null || nodes.Count == 0)
                {
                    LogError("Addresses not found");
                    return;
                }

                Node = new List<Node>();
                foreach (var node in nodes)
                {
                    Node.Add(new Node(node));
                }
            }

            public List<Node> Node { get; }
            public DateTime Threshold { get; }

            public bool Valid => Node?.Count > 0 && Threshold != default;

            public static Configuration FromCommandLineArguments(string[] args)
            {
                string threshold = null;
                var addresses = new List<string>();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-a")
                        addresses.Add(args[i + 1]);
                    else if (args[i] == "-t")
                        threshold = args[i + 1];
                }

                return new Configuration(addresses, threshold);
            }

            public static Configuration FromJsonFile(string filename)
            {
                var obj = JsonConvert.DeserializeAnonymousType(File.ReadAllText(filename), new
                {
                    addresses = new List<string>(),
                    threshold = string.Empty,
                });
                return new Configuration(obj.addresses, obj.threshold);
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
