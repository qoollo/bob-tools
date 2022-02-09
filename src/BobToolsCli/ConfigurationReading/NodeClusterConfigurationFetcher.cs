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
        private readonly NodePortStorage _nodePortStorage;
        public NodeClusterConfigurationFetcher(NodePortStorage nodePortStorage)
        {
            _nodePortStorage = nodePortStorage;
        }

        public async Task<ConfigurationReadingResult<ClusterConfiguration>> GetConfigurationFromNode(string host, int port, CancellationToken cancellationToken)
        {
            var client = new BobApiClient(new Uri($"http://{host}:{port}"));
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
                var nodeClient = new BobApiClient(_nodePortStorage.GetNodeApiUri(node));
                var nodeDisksResult = await nodeClient.GetDisks(cancellationToken);
                if (nodeDisksResult.IsOk(out var nodeDisks, out var nodeDisksError))
                {
                    var resultNode = new ClusterConfiguration.Node
                    {
                        Address = node.Address,
                        Name = node.Name,
                        Disks = nodeDisks.Select(Convert).ToList()
                    };
                    result.Add(resultNode);
                }
                else
                    return ConfigurationReadingResult<List<ClusterConfiguration.Node>>
                        .Error($"Error fetching disks for node {node.Name}: {nodeDisksError}");
            }
            return ConfigurationReadingResult<List<ClusterConfiguration.Node>>.Ok(result);
        }

        private static List<ClusterConfiguration.VDisk> GetVDisks(List<Node> nodes)
        {
            return nodes.SelectMany(n => n.VDisks.Select(Convert)).ToList();
        }

        private static ClusterConfiguration.VDisk Convert(VDisk vdisk)
        {
            return new ClusterConfiguration.VDisk
            {
                Id = vdisk.Id,
                Replicas = vdisk.Replicas.Select(r => new ClusterConfiguration.VDisk.Replica { Disk = r.Disk, Node = r.Node }).ToList()
            };
        }

        private static ClusterConfiguration.Node.Disk Convert(Disk d)
        {
            return new ClusterConfiguration.Node.Disk { Name = d.Name, Path = d.Path };
        }

        private static ClusterConfiguration.Node GetNodeProto(Node node)
        {
            return new ClusterConfiguration.Node
            {
                Address = node.Address,
                Name = node.Name,
                Disks = new List<ClusterConfiguration.Node.Disk>()
            };
        }
    }
}