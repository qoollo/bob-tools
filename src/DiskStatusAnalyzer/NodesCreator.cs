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

        public async Task<List<NodeWithDirs>> CreateNodeStructures(Configuration config)
        {
            var nodes = new List<NodeWithDirs>();
            foreach (var inputNode in config.Nodes)
            {
                logger.LogInformation($"Creating node {inputNode}");
                var info = GetConnectionInfo(config, inputNode);
                var node = await CreateNode(info);
                if (node != null)
                {
                    logger.LogInformation($"Successfully created node {inputNode}");
                    nodes.Add(node);
                }
            }
            return nodes;
        }

        private ConnectionInfo GetConnectionInfo(Configuration config, Configuration.NodeInfo info)
        {
            var hostUri = new Uri($"http://{info.Host}");
            return new ConnectionInfo(
                hostUri,
                info.SshPort,
                new Uri($"http://{info.InnerNetworkHost}"),
                config);
        }

        private async Task<NodeWithDirs> CreateNode(ConnectionInfo info)
        {
            var api = new BobApiClient(info.Uri);
            var status = await api.GetStatus();
            if (status == null)
                return null;
            var diskDirs = new List<DiskDir>();
            foreach (var vDisk in status?.VDisks)
            {
                var dirs = await api.GetDirectories(vDisk);
                foreach (var replica in vDisk.Replicas)
                {
                    if (replica.Node != status?.Name)
                        continue;
                    var parentPath = replica.Path;
                    var name = $"path {replica.Path} on node {info.Uri}";
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
                        var dir = nodeStructureCreator.ParseDisk(replica.Disk, baseDir, info);
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
            var alienDir = await api.GetAlienDirectory();
            var alien = nodeStructureCreator.ParseAlien(alienDir, info);
            return new NodeWithDirs(info.Uri, status?.Name, diskDirs, alien);
        }
    }
}
