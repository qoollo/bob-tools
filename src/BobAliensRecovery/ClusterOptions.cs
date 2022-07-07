using System;
using System.Collections.Generic;
using System.Linq;
using BobApi.BobEntities;
using BobToolsCli.Helpers;

namespace BobAliensRecovery
{
    class ClusterOptions
    {
        private readonly BobApiClientProvider _bobApiClientProvider;

        public ClusterOptions(BobApiClientProvider bobApiClientProvider)
        {
            _bobApiClientProvider = bobApiClientProvider;
        }

        public Uri GetNodeApiUri(ClusterConfiguration.Node node)
        {
            return _bobApiClientProvider.GetNodeApiUri(node);
        }
    }
}
