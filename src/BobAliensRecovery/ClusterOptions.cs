using System;
using System.Collections.Generic;
using System.Linq;
using BobApi.BobEntities;
using BobApi.Helpers;

namespace BobAliensRecovery
{
    class ClusterOptions
    {
        private readonly Dictionary<string, int> _portByNodeName;
        private readonly int _defaultPort;

        public ClusterOptions(IEnumerable<string> portOverrides)
        {
            (_portByNodeName, _defaultPort) = ApiPortOverridesParser.Parse(portOverrides);
        }

        public Uri GetNodeApiUri(ClusterConfiguration.Node node)
        {
            return new Uri("http://" + node.GetIPAddress() + ':' + GetApiPort(node));
        }

        private int GetApiPort(ClusterConfiguration.Node node)
        {
            if (_portByNodeName.TryGetValue(node.Name, out var port))
                return port;
            return _defaultPort;
        }
    }
}