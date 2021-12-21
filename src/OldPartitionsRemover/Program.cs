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
            var storage = provider.GetRequiredService<NodePortStorage>();

            var thresholdResult = args.GetThreshold();
            if (!thresholdResult.IsOk(out var threshold, out var thresholdError))
            {
                System.Console.WriteLine($"Failed to get threshold: {thresholdError}");
                return;
            }
            else
            {
                var clusterConfigurationResult = await args.FindClusterConfiguration(cancellationToken);
                if (!clusterConfigurationResult.IsOk(out var clusterConfiguration, out var error))
                {
                    System.Console.WriteLine($"Failed to read cluster configuration: {error}");
                    return;
                }
                foreach (var node in clusterConfiguration.Nodes)
                {
                    var client = new BobApiClient(storage.GetNodeApiUri(node));
                    var vdisksOnNode = clusterConfiguration.VDisks.Where(vd => vd.Replicas.Any(r => r.Node == node.Name));
                    foreach (var vdisk in vdisksOnNode)
                    {
                        var partitionsResult = await client.GetPartitions(vdisk, cancellationToken);
                        var deletionResult = await partitionsResult.Bind(async partitions =>
                        {
                            var partitionInfosResult = BobApiResult<Partition>.Traverse(await Task.WhenAll(partitions.Select(p => client.GetPartition(vdisk.Id, p, cancellationToken))));
                            return await partitionInfosResult.Bind(async partitionInfos =>
                            {
                                var oldTimestamps = partitionInfos.Select(p => p.Timestamp).Where(p => DateTimeOffset.FromUnixTimeSeconds(p) < threshold);
                                return await oldTimestamps.Aggregate(
                                    Task.FromResult(BobApiResult<bool>.Ok(true)),
                                    (m, ts) => Combine(m, () => client.DeletePartitionsByTimestamp(vdisk.Id, ts, cancellationToken)));
                            });
                        });
                        if (!deletionResult.IsOk(out var res, out var e))
                        {
                            System.Console.WriteLine($"Failed to delete partitions on vdisk {vdisk.Id}, node {node.Name}: {e}");
                            if (!args.ContinueOnError)
                                return;
                        }
                        else if (!res)
                        {
                            System.Console.WriteLine($"Failed to delete partitions on vdisk {vdisk.Id}, node {node.Name}: Bob error");
                            if (!args.ContinueOnError)
                                return;
                        }
                    }
                }
            }

            static Task<BobApiResult<bool>> Combine(Task<BobApiResult<bool>> r, Func<Task<BobApiResult<bool>>> f)
                => r.ContinueWith(t => BobApiResult<BobApiResult<bool>>.Traverse(t.Result.Map(_ => f()))
                        .ContinueWith(t => t.Result.Bind(_ => _))).Unwrap();
        }

        // private static async Task RemoveOldPartitions()
        // {
        //     if (configuration?.Valid != true)
        //     {
        //         LogError("Bad configuration");
        //         return;
        //     }

        //     foreach (var node in configuration.Node)
        //     {
        //         using var api = new BobApiClient(node.Address);
        //         var statusResult = await api.GetStatus();
        //         if (!statusResult.TryGetData(out var status))
        //             continue;
        //         foreach (var vDisk in status.VDisks)
        //         {
        //             foreach (var replica in vDisk.Replicas)
        //             {
        //                 if (replica.Node == status.Name)
        //                 {
        //                     LogInfo($"Processing replica of vdisk {vDisk.Id} on node {node}");
        //                     var partitionsResult = await api.GetPartitions(vDisk);
        //                     if (!partitionsResult.TryGetData(out var partitions))
        //                         LogError($"Partitions for {vDisk} not found");
        //                     else
        //                     {
        //                         LogInfo($"Found {partitions.Count} partitions on {vDisk}");
        //                         await DeleteOldPartitions(node.Address, vDisk, partitions);
        //                     }
        //                 }
        //             }
        //         }
        //     }
        // }

        // private static async Task DeleteOldPartitions(Uri uri, VDisk vDisk, List<string> partitions)
        // {
        //     using var api = new BobApiClient(uri);
        //     foreach (var partitionId in partitions)
        //     {
        //         var partitionResult = await api.GetPartition(vDisk, partitionId);
        //         if (!partitionResult.TryGetData(out var partition))
        //             LogError($"Failed to get partition {partitionId} on {vDisk}");
        //         else if (GetDateTimeFromTimestamp(partition.Timestamp) < configuration.Threshold)
        //         {
        //             await api.DeletePartition(vDisk, partition.Timestamp);
        //             LogInfo($"Deleted partition {partitionId} on {vDisk}");
        //         }
        //     }
        // }

        // private static DateTime GetDateTimeFromTimestamp(long? p) => new DateTime(1970, 1, 1).AddSeconds(p.GetValueOrDefault(0));

        // private static void LogError(string text)
        // {
        //     if (errorEnabled)
        //         Console.WriteLine($"ERROR: {text}");
        // }

        // private static void LogInfo(string text)
        // {
        //     if (infoEnabled)
        //         Console.WriteLine($"INFO: {text}");
        // }

        // private class Configuration
        // {
        //     private static readonly Regex DaysOffsetRegex = new Regex(@"^\-(\d+)d$");

        //     private Configuration(List<string> nodes, string threshold) : this(nodes)
        //     {
        //         if (threshold is null)
        //             LogError("Threshold not found");
        //         if (nodes == null || nodes.Count == 0 || threshold == null) return;

        //         if (DaysOffsetRegex.IsMatch(threshold))
        //             Threshold = DateTime.Now - TimeSpan.FromDays(
        //                 int.Parse(DaysOffsetRegex.Match(threshold).Groups[1].Value));
        //         else if (DateTime.TryParse(threshold, out var dateTimeThreshold))
        //             Threshold = dateTimeThreshold;
        //         else
        //             LogError("Failed to parse threshold");
        //     }

        //     private Configuration(List<string> nodes)
        //     {
        //         if (nodes is null || nodes.Count == 0)
        //         {
        //             LogError("Addresses not found");
        //             return;
        //         }

        //         Node = new List<Node>();
        //         foreach (var node in nodes)
        //         {
        //             Node.Add(new Node(node));
        //         }
        //     }

        //     public List<Node> Node { get; }
        //     public DateTime Threshold { get; }

        //     public bool Valid => Node?.Count > 0 && Threshold != default;

        //     public static Configuration FromCommandLineArguments(string[] args)
        //     {
        //         string threshold = null;
        //         var addresses = new List<string>();
        //         for (int i = 0; i < args.Length - 1; i++)
        //         {
        //             if (args[i] == "-a")
        //                 addresses.Add(args[i + 1]);
        //             else if (args[i] == "-t")
        //                 threshold = args[i + 1];
        //         }

        //         return new Configuration(addresses, threshold);
        //     }

        //     public static Configuration FromJsonFile(string filename)
        //     {
        //         var obj = JsonConvert.DeserializeAnonymousType(File.ReadAllText(filename), new
        //         {
        //             addresses = new List<string>(),
        //             threshold = string.Empty,
        //         });
        //         return new Configuration(obj.addresses, obj.threshold);
        //     }
        // }

        // private struct Node
        // {
        //     public Node(string s)
        //     {
        //         if (Uri.TryCreate(s, UriKind.Absolute, out var address))
        //             Address = address;
        //         else
        //             throw new ArgumentException(nameof(Address));
        //     }

        //     public Uri Address { get; }
        //     public override string ToString()
        //     {
        //         return $"{Address}";
        //     }
        // }
    }
}
