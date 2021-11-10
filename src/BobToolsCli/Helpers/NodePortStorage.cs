using System;
using System.Collections.Generic;
using System.Linq;
using BobApi.BobEntities;

namespace BobToolsCli.Helpers
{
    public class NodePortStorage
    {
        private const char NamePortSeparator = ':';
        private const int DefaultPort = 8000;

        private readonly Dictionary<string, int> _portByNodeName;
        private readonly int _defaultPort;

        internal NodePortStorage(IEnumerable<string> portOverrides)
        {
            _portByNodeName = portOverrides.ToDictionary(s => s.Split(NamePortSeparator)[0], s => int.Parse(s.Split(NamePortSeparator)[1]));
            if (!_portByNodeName.TryGetValue("*", out var defaultPort))
                _defaultPort = DefaultPort;
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