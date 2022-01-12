using BobApi;
using BobApi.BobEntities;

namespace BobToolsCli.BobApliClientFactories
{
    public interface IBobApiClientFactory
    {
        public IPartitionsBobApiClient GetPartitionsBobApiClient(ClusterConfiguration.Node node);
    }
}