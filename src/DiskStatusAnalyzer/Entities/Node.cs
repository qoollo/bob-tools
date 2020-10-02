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

        public Node(Uri uri,
                    string name,
                    List<DiskDir> diskDirs)
        {
            Uri = uri;
            Name = name;
            DiskDirs = diskDirs;
        }

        public Uri Uri { get; }
        public string Name { get; }
        public List<DiskDir> DiskDirs { get; }

        public override string ToString()
        {
            return $"Node: {Name} ({Uri.Host}:{Uri.Port})";
        }
    }
}
