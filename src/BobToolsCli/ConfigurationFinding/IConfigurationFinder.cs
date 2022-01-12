using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;

namespace BobToolsCli.ConfigurationFinding
{
    public interface IConfigurationFinder
    {
        Task<YamlReadingResult<ClusterConfiguration>> FindClusterConfiguration(CancellationToken cancellationToken = default);
    }
}