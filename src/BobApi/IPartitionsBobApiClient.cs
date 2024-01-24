using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobApi.Entities;

namespace BobApi
{
    public interface IPartitionsBobApiClient : IDisposable
    {
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
