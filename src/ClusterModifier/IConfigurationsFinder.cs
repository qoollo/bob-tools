using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;

namespace ClusterModifier;

public interface IConfigurationsFinder
{
    Task<ClusterConfiguration> FindNewConfig(CancellationToken cancellationToken);
    Task<ClusterConfiguration> FindOldConfig(CancellationToken cancellationToken);
}

