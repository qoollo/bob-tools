using System.Collections.Generic;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.ByDateRemoving.Entities
{
    internal readonly struct PartitionFunctions
    {
        public delegate Task<Result<Partition>> PartitionFinder(string id);
        public delegate Task<Result<bool>> PartitionsRemover(long timestamp);
        public delegate Task<Result<List<string>>> PartitionIdsFinder();

        public PartitionFunctions(ClusterConfiguration.VDisk vdisk, NodeApi nodeApi)
        {
            FindPartitionById = async id => await nodeApi.Invoke((c, t) => c.GetPartition(vdisk.Id, id, t));
            RemovePartitionsByTimestamp = async ts =>
            {
                Result<bool> result = await nodeApi.Invoke((c, t) => c.DeletePartitionsByTimestamp(vdisk.Id, ts, t));
                return result.Bind(r => r ? Result<bool>.Ok(true) : Result<bool>.Error("Failed to remove partitions though the bob API"));
            };
            FindPartitionIds = async () => await nodeApi.Invoke((c, t) => c.GetPartitions(vdisk, t));
        }

        public PartitionFinder FindPartitionById { get; }
        public PartitionsRemover RemovePartitionsByTimestamp { get; }
        public PartitionIdsFinder FindPartitionIds { get; }
    }
}