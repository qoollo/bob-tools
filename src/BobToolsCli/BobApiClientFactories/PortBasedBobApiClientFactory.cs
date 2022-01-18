using BobApi;
using BobApi.BobEntities;
using BobToolsCli.Helpers;

namespace BobToolsCli.BobApliClientFactories
{
    public class PortBasedBobApiClientFactory : IBobApiClientFactory
    {
        private readonly NodePortStorage _nodePortStorage;

        public PortBasedBobApiClientFactory(NodePortStorage nodePortStorage)
        {
            _nodePortStorage = nodePortStorage;
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
            return new BobApiClient(_nodePortStorage.GetNodeApiUri(node));
        }
    }
}