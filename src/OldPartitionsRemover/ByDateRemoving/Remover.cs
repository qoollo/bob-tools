using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli;
using BobToolsCli.Helpers;
using OldPartitionsRemover.ByDateRemoving.Entities;
using OldPartitionsRemover.Entites;

namespace OldPartitionsRemover.ByDateRemoving
{
    public partial class Remover
    {
        private readonly NodePortStorage _nodePortStorage;
        private readonly Arguments _arguments;

        public Remover(NodePortStorage nodePortStorage, Arguments arguments)
        {
            _nodePortStorage = nodePortStorage;
            _arguments = arguments;
        }

        internal async Task<Result<bool>> RemoveOldPartitions(CancellationToken cancellationToken)
        {
            Result<ClusterConfiguration> configResult = await _arguments.FindClusterConfiguration(cancellationToken);
            return await configResult.Bind(c => RemoveInCluster(c, cancellationToken));
        }

        private async Task<Result<bool>> RemoveInCluster(ClusterConfiguration clusterConfig,
            CancellationToken cancellationToken)
            => await Traverse(clusterConfig.Nodes, (s, node) => RemoveOnNode(clusterConfig, node, cancellationToken));

        private async Task<Result<bool>> RemoveOnNode(ClusterConfiguration clusterConfig,
            ClusterConfiguration.Node node, CancellationToken cancellationToken)
        {
            var vdisksOnNode = clusterConfig.VDisks.Where(vd => vd.Replicas.Any(r => r.Node == node.Name));
            var nodeApi = new NodeApi(new BobApiClient(_nodePortStorage.GetNodeApiUri(node)), cancellationToken);

            return await Traverse(vdisksOnNode, (s, vdisk) => RemoveOnVDisk(vdisk, nodeApi));
        }

        private async Task<Result<bool>> RemoveOnVDisk(ClusterConfiguration.VDisk vdisk, NodeApi nodeApi)
        {
            var partitionFunctions = new PartitionFunctions(vdisk, nodeApi);

            var partitionIdsResult = await partitionFunctions.FindPartitionIds();
            return await partitionIdsResult.Bind(partitionIds => RemoveWithinPatitionIds(partitionIds, partitionFunctions));
        }

        private async Task<Result<bool>> RemoveWithinPatitionIds(List<string> partitionIds, PartitionFunctions partitionFunctions)
        {
            var partitionsResult = await GetPartitions(partitionIds, partitionFunctions.FindPartitionById);
            return await partitionsResult.Bind(partitions => RemoveWithinPartitions(partitions, partitionFunctions.RemovePartitionsByTimestamp));
        }

        private static async Task<Result<List<Partition>>> GetPartitions(List<string> partitionIds,
            PartitionFunctions.PartitionFinder find)
        {
            return await Traverse(partitionIds, new List<Partition>(),
                async (l, p) => (await find(p)).Map(part => { l.Add(part); return l; }));
        }

        private async Task<Result<bool>> RemoveWithinPartitions(List<Partition> partitionInfos,
            PartitionFunctions.PartitionsRemover remove)
        {
            var thresholdResult = _arguments.GetThreshold();
            return await thresholdResult.Bind(async threshold => await RemoveByTimestamp(partitionInfos, remove, threshold));
        }

        private static async Task<Result<bool>> RemoveByTimestamp(List<Partition> partitionInfos,
            PartitionFunctions.PartitionsRemover remove, DateTimeOffset threshold)
        {
            var oldTimestamps = partitionInfos.Select(p => p.Timestamp).Where(p => DateTimeOffset.FromUnixTimeSeconds(p) < threshold);
            return await Traverse(oldTimestamps, async (_, ts) => await remove(ts));
        }

        private static async Task<Result<bool>> Traverse<T>(IEnumerable<T> elems, Func<bool, T, Task<Result<bool>>> f)
            => await Traverse(elems, true, f);

        private static async Task<Result<Y>> Traverse<T, Y>(IEnumerable<T> elems, Y seed, Func<Y, T, Task<Result<Y>>> f)
        {
            return await elems.Aggregate(
                Task.FromResult(Result<Y>.Ok(seed)),
                (task, next) => task.ContinueWith(result => Combine(f, next, result)).Unwrap());
        }

        private static async Task<Result<Y>> Combine<T, Y>(Func<Y, T, Task<Result<Y>>> f, T next, Task<Result<Y>> result)
        {
            var combined = await Result<Result<Y>>.Sequence(result.Result.Map(v => f(v, next)));
            return combined.Bind(_ => _);
        }
    }
}