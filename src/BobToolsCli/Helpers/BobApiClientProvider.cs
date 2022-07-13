using System;
using System.Collections.Generic;
using System.Linq;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;

namespace BobToolsCli.Helpers
{
    public class BobApiClientProvider
    {
        private const char NameSeparator = ':';
        private const int DefaultPort = 8000;

        private readonly Dictionary<string, int> _portByNodeName;
        private readonly Dictionary<string, Credentials> _credsByNodeName;
        private readonly int _defaultPort;
        private readonly Credentials _defaultCreds;

        internal BobApiClientProvider(IEnumerable<string> portOverrides, IEnumerable<string> credentials)
        {
            _portByNodeName = portOverrides.ToDictionary(s => s.Split(NameSeparator)[0], s => int.Parse(s.Split(NameSeparator)[1]));
            if (!_portByNodeName.TryGetValue("*", out _defaultPort))
                _defaultPort = DefaultPort;

            _credsByNodeName = new Dictionary<string, Credentials>();
            foreach(var cred in credentials)
            {
                var nameSplit = cred.Split(NameSeparator);
                if (nameSplit.Length > 2)
                    throw new ArgumentException("Wrong credentials format");

                if (!Credentials.TryParse(nameSplit.Last(), out var creds))
                    throw new ArgumentException("Wrong credentials format");

                if (nameSplit.Length == 1)
                    _defaultCreds = creds;
                else
                    _credsByNodeName.Add(nameSplit[0], creds);
            }
            if (_defaultCreds.Username is null)
                throw new ArgumentException("Default credentials must be specified");
        }


        public BobApiClient GetClient(Node node)
        {
            var uri = node.GetUri();
            if (!_credsByNodeName.TryGetValue(node.Name, out var creds))
                creds = _defaultCreds;
            return new BobApiClient(GetNodeApiUriWithPortOverride(uri, node.Name), creds.Username, creds.Password);
        }

        public BobApiClient GetClient(ClusterConfiguration.Node node)
        {
            var uri = node.GetUri();
            if (!_credsByNodeName.TryGetValue(node.Name, out var creds))
                creds = _defaultCreds;
            return new BobApiClient(GetNodeApiUriWithPortOverride(uri, node.Name), creds.Username, creds.Password);
        }

        public BobApiClient GetClient(string host, int port)
        {
            return new BobApiClient(new Uri($"http://{host}:{port}"), _defaultCreds.Username, _defaultCreds.Password);
        }

        private Uri GetNodeApiUriWithPortOverride(Uri nodeUri, string nodeName)
        {
            var uri = new UriBuilder(nodeUri)
            {
                Port = GetApiPort(nodeName)
            };
            return uri.Uri;
        }

        private int GetApiPort(string nodeName)
        {
            if (_portByNodeName.TryGetValue(nodeName, out var port))
                return port;
            return _defaultPort;
        }

        private struct Credentials
        {
            public Credentials(string username, string password)
            {
                Username = username;
                Password = password;
            }
            
            public string Username { get; }
            public string Password { get; }

            public static bool TryParse(string s, out Credentials result)
            {
                result = default;
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var split = s.Split('=');
                    if (split.Length == 2)
                    {
                        result = new Credentials(split[0], split[1]);
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
