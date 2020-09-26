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
            await RemoveOldPartitions();
        }

        private static async Task RemoveOldPartitions()
        {
            if (configuration is null || configuration.Nodes.Count == 0)
            {
                LogError("Bad configuration");
                return;
            }

            ulong recordsCountWithReplicas = 0;
            var countByVdiskId = new Dictionary<int, ulong>();
            foreach (var node in configuration.Nodes)
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
                                ulong count = await CountRecordsOnReplica(node.Address, vDisk, partitions);
                                recordsCountWithReplicas += count;
                                if (countByVdiskId.ContainsKey(vDisk.Id))
                                    countByVdiskId[vDisk.Id] = Math.Max(countByVdiskId[vDisk.Id], count);
                                else
                                    countByVdiskId.Add(vDisk.Id, count);
                            }
                        }
                    }
                }
            }

            var recordsCount = countByVdiskId.Values.Aggregate((ulong)0, (s, n) => s + n);
            Console.WriteLine($"Total records count: {recordsCount}");
            Console.WriteLine($"Total records count with replicas: {recordsCountWithReplicas}");
        }

        private static async Task<ulong> CountRecordsOnReplica(Uri uri, VDisk vDisk, List<int> partitions)
        {
            using var api = new BobApiClient(uri);
            var vDiskRecordsByPartitions = new Dictionary<int, ulong>();
            foreach (var partition in partitions)
            {
                var partitionObject = await api.GetPartition(vDisk, partition);
                if (partitionObject is null)
                    LogError($"Failed to get partition {partition} on {vDisk}");
                else
                {
                    LogInfo(
                        $"Found {partitionObject.RecordsCount} records on partition {partition} on {vDisk}");
                    if (vDiskRecordsByPartitions.ContainsKey(partition))
                        vDiskRecordsByPartitions[partition] =
                            vDiskRecordsByPartitions[partition] + partitionObject.RecordsCount;
                    else
                        vDiskRecordsByPartitions.Add(partition, partitionObject.RecordsCount);
                }
            }

            return vDiskRecordsByPartitions.Values.Aggregate((ulong)0, (s, n) => s + n);
        }

        private static DateTime GetDateTimeFromTimestamp(int p) => new DateTime(1970, 1, 1).AddSeconds(p);

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
