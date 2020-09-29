using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.Rsync;
using Microsoft.Extensions.Logging;

namespace DiskStatusAnalyzer
{
    public class NodesCreator
    {
        private readonly ILogger<NodesCreator> logger;
        private readonly ILogger<Node> nodeLogger;
        private readonly ILogger<RsyncWrapper> rsyncWrapperLogger;
        private readonly ILoggerFactory loggerFactory;

        public NodesCreator(ILogger<NodesCreator> logger, ILogger<Node> nodeLogger, ILogger<RsyncWrapper> rsyncWrapperLogger, ILoggerFactory loggerFactory)
        {
            this.logger = logger;
            this.nodeLogger = nodeLogger;
            this.rsyncWrapperLogger = rsyncWrapperLogger;
            this.loggerFactory = loggerFactory;
        }

        public async Task<List<Node>> CreateNodeStructures(Configuration config)
        {
            var nodes = new List<Node>();
            foreach (var inputNode in config.Nodes)
            {
                var node = CreateNode(config, inputNode);
                logger.LogInformation($"Connecting to node {node.Uri}");
                try
                {
                    if (await node.Initialize())
                        nodes.Add(node);
                    else
                        throw new Exception();
                }
                catch
                {
                    logger.LogError($"Failed to connect");
                }
            }

            return nodes;
        }

        private Node CreateNode(Configuration config, Configuration.Node inputNode)
        {
            var hostUri = new Uri($"http://{inputNode.Host}");
            var rsyncWrapper = new RsyncWrapper(inputNode.SshPort, hostUri,
                new Uri($"http://{inputNode.InnerNetworkHost}"),
                config, rsyncWrapperLogger);
            var node = new Node(hostUri, rsyncWrapper, nodeLogger, loggerFactory);
            return node;
        }

    }
}
