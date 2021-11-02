using System.Collections.Generic;
using System.Linq;

namespace BobApi.Helpers
{
    public static class ApiPortOverridesParser
    {
        private const char NamePortSeparator = ':';
        private const int DefaultPort = 8000;

        public const string HelpText = "Override default api port for the node. E.g. node1:80,node2:8000. Wildcard char (*) can be used to set port for all nodes.";

        public static (Dictionary<string, int> portByNodeName, int defaultPort) Parse(IEnumerable<string> portOverrides)
        {
            var portByNodeName = portOverrides.ToDictionary(s => s.Split(NamePortSeparator)[0], s => int.Parse(s.Split(NamePortSeparator)[1]));
            if (!portByNodeName.TryGetValue("*", out var defaultPort))
                defaultPort = DefaultPort;
            return (portByNodeName, defaultPort);
        }
    }
}