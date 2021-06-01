using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DiskStatusAnalyzer.NodeStructureCreation;
using DiskStatusAnalyzer.Rsync;
using DiskStatusAnalyzer.Rsync.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiskStatusAnalyzer.Entities
{
    public class NodeWithDirs
    {
        public NodeWithDirs(ConnectionInfo connectionInfo,
                    string name,
                    List<DiskDir> diskDirs,
                    AlienDir alienDir)
        {
            Uri = connectionInfo.Uri;
            ConnectionInfo = connectionInfo;
            Name = name;
            DiskDirs = diskDirs;
            AlienDir = alienDir;
        }

        public Uri Uri { get; }
        public ConnectionInfo ConnectionInfo { get; }
        public string Name { get; }
        public List<DiskDir> DiskDirs { get; }
        public AlienDir AlienDir { get; }

        public override string ToString()
        {
            return $"Node: {Name} ({Uri.Host}:{Uri.Port})";
        }
    }
}
