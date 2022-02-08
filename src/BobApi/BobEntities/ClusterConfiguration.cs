using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace BobApi.BobEntities
{
    public class ClusterConfiguration
    {
        [YamlMember(Alias = "nodes")]
        public List<Node> Nodes { get; set; }

        [YamlMember(Alias = "vdisks")]
        public List<VDisk> VDisks { get; set; }

        public class Node
        {
            [YamlMember(Alias = "name")]
            public string Name { get; set; }

            [YamlMember(Alias = "address")]
            public string Address { get; set; }

            [YamlMember(Alias = "disks")]
            public List<Disk> Disks { get; set; }

            public Uri GetUri() => new Uri($"http://{Address}");

            public async ValueTask<IPAddress> FindIPAddress()
            {
                var host = GetUri().Host;
                if (!IPAddress.TryParse(host, out var addr))
                    addr = (await Dns.GetHostAddressesAsync(host)).FirstOrDefault();
                return addr;

            }

            public class Disk
            {
                [YamlMember(Alias = "name")]
                public string Name { get; set; }

                [YamlMember(Alias = "path")]
                public string Path { get; set; }
            }

            public override string ToString()
            {
                return Name;
            }
        }

        public class VDisk
        {
            [YamlMember(Alias = "id")]
            public long Id { get; set; }

            [YamlMember(Alias = "replicas")]
            public List<Replica> Replicas { get; set; }

            public class Replica
            {
                [YamlMember(Alias = "node")]
                public string Node { get; set; }

                [YamlMember(Alias = "disk")]
                public string Disk { get; set; }
            }
        }

        public string FindDiskName(string nodeName, long vdiskId)
        {
            return VDisks.Find(vd => vd.Id == vdiskId)?.Replicas.Find(r => r.Node == nodeName)?.Disk;
        }

        public static async Task<ClusterConfiguration> FromYamlFile(string filename)
        {
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var content = await File.ReadAllTextAsync(filename);
            return deserializer.Deserialize<ClusterConfiguration>(content);
        }
    }
}

