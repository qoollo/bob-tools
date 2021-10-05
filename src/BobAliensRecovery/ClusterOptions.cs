using System;
using System.Collections.Generic;
using System.Linq;
using BobApi.BobEntities;

namespace BobAliensRecovery
{
    class ClusterOptions
    {
        private const string WildcardChar = "*";

        public ClusterOptions(IEnumerable<(string name, int port)> portOverrides)
        {
            ClusterNodesPortOverrides = portOverrides.ToDictionary(t => t.name, t => t.port);
        }

        public Dictionary<string, int> ClusterNodesPortOverrides { get; } = new Dictionary<string, int>();

        public Uri GetNodeApiUri(ClusterConfiguration.Node node)
        {
            return new Uri("http://" + node.GetIPAddress() + ':' + GetApiPort(node));
        }

        private int GetApiPort(ClusterConfiguration.Node node)
        {
            if (ClusterNodesPortOverrides.TryGetValue(node.Name, out var port))
                return port;
            else if (ClusterNodesPortOverrides.TryGetValue(WildcardChar, out port))
                return port;
            return 8000;
        }
    }
}