using System.Collections.Generic;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.ByDateRemoving.Entities
{
    internal class PartitionFunctions
    {
        private readonly ClusterConfiguration.VDisk _vdisk;
        private readonly NodeApi _nodeApi;

        public PartitionFunctions(ClusterConfiguration.VDisk vdisk, NodeApi nodeApi)
        {
            _vdisk = vdisk;
            _nodeApi = nodeApi;
        }

        public async Task<Result<Partition>> FindPartitionById(string id)
        {
            return await _nodeApi.Invoke((c, t) => c.GetPartition(_vdisk.Id, id, t));
        }
        public async Task<Result<bool>> RemovePartitionsByTimestamp(long timestamp)
        {
            Result<bool> result = await _nodeApi.Invoke((c, t) => c.DeletePartitionsByTimestamp(_vdisk.Id, timestamp, t));
            return result.Bind(r => r ? Result<bool>.Ok(true) : Result<bool>.Error("Failed to remove partitions though the bob API"));
        }

        public async Task<Result<List<string>>> FindPartitionIds()
        {
            return await _nodeApi.Invoke((c, t) => c.GetPartitions(_vdisk, t));
        }
    }
}
