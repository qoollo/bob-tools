using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DiskStatusAnalyzer.NodeStructureCreation;
using DiskStatusAnalyzer.Rsync;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiskStatusAnalyzer.Entities
{
    public class Node
    {
        private readonly RsyncWrapper rsyncWrapper;
        private readonly ILogger<Node> logger;
        private readonly ILoggerFactory loggerFactory;

        public Node(Uri uri, RsyncWrapper rsyncWrapper, ILogger<Node> logger,
                    ILoggerFactory loggerFactory)
        {
            Uri = uri;
            this.rsyncWrapper = rsyncWrapper;
            this.logger = logger;
            this.loggerFactory = loggerFactory;
        }

        public Uri Uri { get; }
        public string Name { get; private set; }

        public List<DiskDir> DiskDirs { get; } = new List<DiskDir>();

        public async Task<bool> Initialize()
        {
            BobNode node;
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = Uri;
                var res = await httpClient.GetAsync("status");
                if (!res.IsSuccessStatusCode)
                    return false;
                node = await res.Content.ReadAsStringAsync()
                    .ContinueWith(t => JsonConvert.DeserializeObject<BobNode>(t.Result));
                if (node is null)
                    return false;
            }
            logger.LogInformation($"Successfully read status from node {Uri}, creating disks structure...");
            Name = node.Name;
            var structureCreator = new NodeStructureCreator(rsyncWrapper,
                                                            loggerFactory.CreateLogger<NodeStructureCreator>());
            foreach (var vDisk in node.VDisks)
            {
                foreach (var replica in vDisk.Replicas)
                {
                    if (replica.Node != Name)
                        continue;
                    var parentPath = replica.Path.TrimEnd('/').Substring(0, replica.Path.LastIndexOf('/'));
                    logger.LogInformation($"Reading disk structure from path {replica.Path}");
                    var dirs = await rsyncWrapper.GetDirectories(parentPath);
                    var rsyncEntry = dirs.FirstOrDefault(re => re.Path.TrimEnd('/') == replica.Path.TrimEnd('/'));
                    if (rsyncEntry != null)
                    {
                        logger.LogInformation($"Disk {replica.Path} found");
                        var dir = structureCreator.ParseDisk(rsyncEntry);
                        if (dir != null)
                        {
                            logger.LogInformation($"Successfully read structure of disk {replica.Path}");
                            DiskDirs.Add(dir);
                        }
                        else
                            logger.LogError($"Structure of disk {replica.Path} can't be parsed");
                    }
                    else
                        logger.LogError($"Disk {replica.Path} not found");
                }
            }

            return true;
        }

        private class BobNode
        {
            public string Name { get; set; }
            public string Address { get; set; }
            public List<BobVDisk> VDisks { get; set; }
        }

        private class BobVDisk
        {
            public int Id { get; set; }
            public List<BobReplica> Replicas { get; set; }
        }

        private class BobReplica
        {
            public string Node { get; set; }
            public string Disk { get; set; }
            public string Path { get; set; }
        }

        public override string ToString()
        {
            return $"Node: {Name} ({Uri.Host}:{Uri.Port})";
        }
    }
}
