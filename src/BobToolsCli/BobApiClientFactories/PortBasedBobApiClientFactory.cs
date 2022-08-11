using BobApi;
using BobApi.BobEntities;
using BobToolsCli.Helpers;

namespace BobToolsCli.BobApliClientFactories
{
    public class PortBasedBobApiClientFactory : IBobApiClientFactory
    {
        private readonly BobApiClientProvider _bobApiClientProvider;

        public PortBasedBobApiClientFactory(BobApiClientProvider bobApiClientProvider)
        {
            _bobApiClientProvider = bobApiClientProvider;
        }

        public IPartitionsBobApiClient GetPartitionsBobApiClient(ClusterConfiguration.Node node)
        {
            return GetBobApiClient(node);
        }

        public ISpaceBobApiClient GetSpaceBobApiClient(ClusterConfiguration.Node node)
        {
            return GetBobApiClient(node);
        }

        private BobApiClient GetBobApiClient(ClusterConfiguration.Node node)
        {
            return _bobApiClientProvider.GetClient(node);
        }
    }
}
