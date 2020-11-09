using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BobApi;
using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.NodeStructureCreation;
using DiskStatusAnalyzer.Rsync;
using DiskStatusAnalyzer.Rsync.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiskStatusAnalyzer
{
    public class NodesCreator
    {
        private readonly ILogger<NodesCreator> logger;
        private readonly NodeStructureCreator nodeStructureCreator;

        public NodesCreator(ILogger<NodesCreator> logger, NodeStructureCreator nodeStructureCreator)
        {
            this.logger = logger;
            this.nodeStructureCreator = nodeStructureCreator;
        }

        public async Task<List<Node>> CreateNodeStructures(Configuration config)
        {
            var nodes = new List<Node>();
            foreach (var inputNode in config.Nodes)
            {
                logger.LogInformation($"Creating node {inputNode}");
                var node = await CreateNode(config, inputNode);
                if (node != null)
                {
                    logger.LogInformation($"Successfully created node {inputNode}");
                    nodes.Add(node);
                }
            }
            return nodes;
        }

        private async Task<Node> CreateNode(Configuration config, Configuration.Node inputNode)
        {
            var hostUri = new Uri($"http://{inputNode.Host}");
            var api = new BobApiClient(hostUri);
            var status = await api.GetStatus();
            if (status == null)
                return null;
            var connectionConfiguration = new ConnectionInfo(
                hostUri,
                inputNode.SshPort,
                new Uri($"http://{inputNode.InnerNetworkHost}"),
                config);
            var diskDirs = new List<DiskDir>();
            foreach (var vDisk in status?.VDisks)
            {
                var dirs = await api.GetDirectories(vDisk);
                foreach (var replica in vDisk.Replicas)
                {
                    if (replica.Node != status?.Name)
                        continue;
                    var parentPath = replica.Path;
                    var name = $"path {replica.Path} on node {hostUri}";
                    logger.LogInformation($"Reading disk structure from {name}");
                    if (dirs == null)
                    {
                        logger.LogWarning($"Failed to get disk structure for {name}");
                        continue;
                    }
                    var baseDir = dirs.FirstOrDefault(re => re.Path.TrimEnd('/') == replica.Path.TrimEnd('/'));
                    if (baseDir.Path != null)
                    {
                        logger.LogInformation($"Found dir for {name}");
                        var dir = nodeStructureCreator.ParseDisk(baseDir, connectionConfiguration);
                        if (dir != null)
                        {
                            logger.LogInformation($"Successfully read structure of {name}");
                            diskDirs.Add(dir);
                        }
                        else
                            logger.LogWarning($"Structure of {name} can't be parsed");
                    }
                    else
                        logger.LogWarning($"Dir for {name} no found");
                }
            }
            return new Node(hostUri, status?.Name, diskDirs);
        }
    }
}
