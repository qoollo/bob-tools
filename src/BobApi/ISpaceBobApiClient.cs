using System.Threading;
using System.Threading.Tasks;
using BobApi.Entities;

namespace BobApi
{
    public interface ISpaceBobApiClient
    {
        Task<BobApiResult<ulong>> GetFreeSpaceBytes(CancellationToken cancellationToken = default);
        Task<BobApiResult<ulong>> GetOccupiedSpaceBytes(CancellationToken cancellationToken = default);
    }
}