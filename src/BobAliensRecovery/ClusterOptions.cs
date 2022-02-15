using System;
using System.Collections.Generic;
using System.Linq;
using BobApi.BobEntities;
using BobToolsCli.Helpers;

namespace BobAliensRecovery
{
    class ClusterOptions
    {
        private readonly NodePortStorage _nodePortStorage;

        public ClusterOptions(NodePortStorage nodePortStorage)
        {
            _nodePortStorage = nodePortStorage;
        }

        public Uri GetNodeApiUri(ClusterConfiguration.Node node)
        {
            return _nodePortStorage.GetNodeApiUri(node);
        }
    }
}