using BobApi;
using BobApi.BobEntities;

namespace BobToolsCli.BobApliClientFactories
{
    public interface IBobApiClientFactory
    {
        IPartitionsBobApiClient GetPartitionsBobApiClient(ClusterConfiguration.Node node);
        ISpaceBobApiClient GetSpaceBobApiClient(ClusterConfiguration.Node node);
    }
}