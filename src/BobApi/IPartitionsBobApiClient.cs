using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobApi.Entities;

namespace BobApi
{
    public interface IPartitionsBobApiClient
    {
        Task<BobApiResult<bool>> DeletePartitionsByTimestamp(
            long vDiskId,
            long timestamp,
            CancellationToken cancellationToken = default
        );
        Task<BobApiResult<Partition>> GetPartition(
            long vdiskId,
            string partition,
            CancellationToken cancellationToken = default
        );
        Task<BobApiResult<List<string>>> GetPartitions(
            ClusterConfiguration.VDisk vDisk,
            CancellationToken cancellationToken = default
        );
        Task<BobApiResult<List<PartitionSlim>>> GetPartitionSlims(
            string diskName,
            long vDiskId,
            CancellationToken cancellationToken = default
        );
        Task<BobApiResult<List<PartitionSlim>>> GetAlienPartitionSlims(
            string nodeName,
            long vDiskId,
            CancellationToken cancellationToken = default
        );
        Task<BobApiResult<bool>> DeletePartitionById(
            string diskName,
            long vDiskId,
            string partitionId,
            CancellationToken cancellationToken
        );
        Task<BobApiResult<bool>> DeleteAlienPartitionById(
            string nodeName,
            long vDiskId,
            string partitionId,
            CancellationToken cancellationToken
        );
    }
}
