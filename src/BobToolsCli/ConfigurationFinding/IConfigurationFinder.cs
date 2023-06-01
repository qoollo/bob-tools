using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.ConfigurationReading;

namespace BobToolsCli.ConfigurationFinding
{
    public interface IConfigurationFinder
    {
        Task<ConfigurationReadingResult<ClusterConfiguration>> FindClusterConfiguration(bool skipErrors = false, CancellationToken cancellationToken = default);
    }
}
