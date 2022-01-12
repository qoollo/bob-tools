using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobApi.Entities;

namespace BobApi
{
    public interface IPartitionsBobApiClient
    {
        Task<BobApiResult<bool>> DeletePartitionsByTimestamp(long vDiskId, long timestamp, CancellationToken cancellationToken = default);
        Task<BobApiResult<Partition>> GetPartition(long id, string partition, CancellationToken cancellationToken = default);
        Task<BobApiResult<List<string>>> GetPartitions(ClusterConfiguration.VDisk vDisk, CancellationToken cancellationToken = default);
    }
}
