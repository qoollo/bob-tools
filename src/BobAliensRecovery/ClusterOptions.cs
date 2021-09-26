using System.Collections.Generic;
using System.Linq;
using BobApi.BobEntities;

namespace BobAliensRecovery
{
    class ClusterOptions
    {
        public ClusterOptions(IEnumerable<(string name, int port)> portOverrides)
        {
            ClusterNodesPortOverrides = portOverrides.ToDictionary(t => t.name, t => t.port);
        }

        public Dictionary<string, int> ClusterNodesPortOverrides { get; } = new Dictionary<string, int>();

        public int GetApiPort(ClusterConfiguration.Node node)
        {
            if (ClusterNodesPortOverrides.TryGetValue(node.Name, out var port))
                return port;
            return 8000;
        }
    }
}