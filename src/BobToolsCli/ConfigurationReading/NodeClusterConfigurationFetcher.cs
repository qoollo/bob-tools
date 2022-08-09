using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli.Helpers;

namespace BobToolsCli.ConfigurationReading
{
    internal class NodeClusterConfigurationFetcher
    {
        private readonly BobApiClientProvider _bobApiClientProvider;
        public NodeClusterConfigurationFetcher(BobApiClientProvider bobApiClientProvider)
        {
            _bobApiClientProvider = bobApiClientProvider;
        }

        public async Task<ConfigurationReadingResult<ClusterConfiguration>> GetConfigurationFromNode(string host, int port, CancellationToken cancellationToken)
        {
            var client = _bobApiClientProvider.GetClient(host, port);
            var nodesResult = await client.GetNodes(cancellationToken);
            if (nodesResult.IsOk(out var nodes, out var nodesError))
            {
                var resultVdisks = GetVDisks(nodes);
                var resultNodesRes = await GetNodes(nodes, cancellationToken);
                return resultNodesRes.Map(resultNodes => new ClusterConfiguration { Nodes = resultNodes, VDisks = resultVdisks });
            }
            else
                return ConfigurationReadingResult<ClusterConfiguration>.Error($"Error getting nodes: {nodesError}");
        }

        private async Task<ConfigurationReadingResult<List<ClusterConfiguration.Node>>> GetNodes(List<Node> nodes, CancellationToken cancellationToken)
        {
            var result = new List<ClusterConfiguration.Node>();
            foreach (var node in nodes)
            {
                using var nodeClient = _bobApiClientProvider.GetClient(node);
                var statusResult = await nodeClient.GetStatus(cancellationToken);
                if (statusResult.IsOk(out var status, out var statusError))
                {
                    if (status.Name == node.Name)
                    {
                        var nodeDisksResult = await nodeClient.GetDisks(cancellationToken);
                        if (nodeDisksResult.IsOk(out var nodeDisks, out var nodeDisksError))
                        {
                            var resultNode = new ClusterConfiguration.Node
                            {
                                Address = node.Address,
                                Name = node.Name,
                                Disks = nodeDisks.GroupBy(d => d.Name).Select(g => g.First()).Select(Convert).ToList()
                            };
                            result.Add(resultNode);
                        }
                        else
                            return ConfigurationReadingResult<List<ClusterConfiguration.Node>>
                                .Error($"Error fetching disks for node {node.Name}: {nodeDisksError}");
                    }
                    else
                        return ConfigurationReadingResult<List<ClusterConfiguration.Node>>
                            .Error($"Expected to find node \"{node.Name}\" but found \"{status.Name}\"");
                }
                else
                    return ConfigurationReadingResult<List<ClusterConfiguration.Node>>
                        .Error($"Error getting status for node {node.Name}: {statusError}");
            }
            return ConfigurationReadingResult<List<ClusterConfiguration.Node>>.Ok(result);
        }

        private static List<ClusterConfiguration.VDisk> GetVDisks(List<Node> nodes)
        {
            var vdisks = new Dictionary<int, ClusterConfiguration.VDisk>();
            foreach (var node in nodes)
            {
                foreach (var vd in node.VDisks)
                {
                    var convertedVdisk = Convert(vd);
                    if (vdisks.TryGetValue(vd.Id, out var vdisk))
                    {
                        var newReplicas = convertedVdisk.Replicas.Where(r => !vdisk.Replicas.Contains(r)).ToArray();
                        vdisk.Replicas.AddRange(newReplicas);
                    }
                    else
                    {
                        vdisks.Add(vd.Id, convertedVdisk);
                    }
                }
            }

            return vdisks.Values.ToList();
        }

        private static ClusterConfiguration.VDisk Convert(VDisk vdisk)
            => new()
            {
                Id = vdisk.Id,
                Replicas = vdisk.Replicas.Select(Convert).ToList()
            };

        private static ClusterConfiguration.VDisk.Replica Convert(Replica r)
            => new() { Disk = r.Disk, Node = r.Node };

        private static ClusterConfiguration.Node.Disk Convert(Disk d)
            => new() { Name = d.Name, Path = d.Path };
    }
}
