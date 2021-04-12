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
    public class NodeWithDirs
    {

        public NodeWithDirs(Uri uri,
                    string name,
                    List<DiskDir> diskDirs,
                    AlienDir alienDir)
        {
            Uri = uri;
            Name = name;
            DiskDirs = diskDirs;
            AlienDir = alienDir;
        }

        public Uri Uri { get; }
        public string Name { get; }
        public List<DiskDir> DiskDirs { get; }
        public AlienDir AlienDir { get; }

        public override string ToString()
        {
            return $"Node: {Name} ({Uri.Host}:{Uri.Port})";
        }
    }
}
