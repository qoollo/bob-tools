using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using YamlDotNet.Serialization;

namespace BobApi.Helpers
{
    public static class BobYamlClusterConfigParser
    {
        public static async Task<ClusterConfiguration> ParseYaml(string filename, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException("Cluster configuration file not found", filename);

            var configContent = await File.ReadAllTextAsync(filename, cancellationToken: cancellationToken);
            return new Deserializer().Deserialize<ClusterConfiguration>(configContent);
        }
    }
}