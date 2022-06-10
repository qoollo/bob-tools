using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
using ByteSizeLib;
using Microsoft.Extensions.Logging;
using OldPartitionsRemover.Entities;
using OldPartitionsRemover.Infrastructure;

namespace OldPartitionsRemover.BySpaceRemoving
{
    public class Remover
    {
        private readonly Arguments _arguments;
        private readonly IConfigurationFinder _configurationFinder;
        private readonly IBobApiClientFactory _bobApiClientFactory;
        private readonly ResultsCombiner _resultsCombiner;
        private readonly ILogger<Remover> _logger;

        public Remover(Arguments arguments, IConfigurationFinder configurationFinder,
            IBobApiClientFactory bobApiClientFactory, ResultsCombiner resultsCombiner,
            ILogger<Remover> logger)
        {
            _arguments = arguments;
            _configurationFinder = configurationFinder;
            _bobApiClientFactory = bobApiClientFactory;
            _resultsCombiner = resultsCombiner;
            _logger = logger;
        }

        public async Task<Result<int>> RemovePartitionsBySpace(CancellationToken cancellationToken)
        {
            Result<ClusterConfiguration> configResult = await _configurationFinder.FindClusterConfiguration(cancellationToken);
            var nodeStats = await configResult.Bind(async conf => await RemoveInCluster(conf, cancellationToken));
            return nodeStats.Map(l =>
            {
                var removed = 0;
                foreach (var stat in l)
                {
                    _logger.LogInformation("Node: {Node}, removed: {Removed}, freed: {Freed}, left: {Left}",
                        stat.Node.Name, stat.RemovedPartitions, stat.FreeSpace - stat.StartSpace, stat.FreeSpace);
                    removed += stat.RemovedPartitions;
                }
                return removed;
            });
        }

        private async Task<Result<List<NodeStat>>> RemoveInCluster(ClusterConfiguration clusterConfiguration,
            CancellationToken cancellationToken)
        {
            var thresholdResult = _arguments.GetThreshold();
            return await thresholdResult.Bind(async bs =>
            {
                return await RemoveInClusterWithLessFreeSpace(clusterConfiguration, bs, cancellationToken);
            });
        }

        private async Task<Result<List<NodeStat>>> RemoveInClusterWithLessFreeSpace(ClusterConfiguration clusterConfiguration,
            ByteSize threshold, CancellationToken cancellationToken)
        {
            return await _resultsCombiner.CollectResults(clusterConfiguration.Nodes, async n =>
            {
                var spaceClient = _bobApiClientFactory.GetSpaceBobApiClient(n);
                Result<ulong> spaceResult = await spaceClient.GetFreeSpaceBytes(cancellationToken);
                return await spaceResult.Map(d => ByteSize.FromBytes(d)).Bind(async space =>
                {
                    _logger.LogInformation("Free space on {Node}: {FreeSpace}", n.Name, space);
                    if (space > threshold)
                        return Result<NodeStat>.Ok(new NodeStat(n, space, true));

                    _logger.LogInformation("The remaining free space threshold on the node {Node} has been reached ({Actual} < {Threshold})",
                        n.Name, space, threshold);
                    var removeResult = await RemoveOnNode(clusterConfiguration, n, threshold, cancellationToken);
                    return removeResult.Map(s => s.WithStartSpace(space));
                });
            });
        }

        private async Task<Result<NodeStat>> RemoveOnNode(ClusterConfiguration clusterConfiguration, ClusterConfiguration.Node node,
            ByteSize threshold, CancellationToken cancellationToken)
        {
            var removalFunctionsResult = await GetRemovalFunctions(clusterConfiguration, node, cancellationToken);
            return await removalFunctionsResult
                .Bind(async removalFunctions => await _resultsCombiner.CombineResults(removalFunctions, new NodeStat(), async (n, f) =>
                {
                    if (n.IsEnoughSpace)
                        return Result<NodeStat>.Ok(n);
                    var deleteResult = await f();
                    return await deleteResult.Bind(async removed =>
                    {
                        var checkResult = await CheckIfEnoughSpace(node, threshold, cancellationToken);
                        return checkResult.Map(r => r.WithRemovedPartitions(n.RemovedPartitions + removed));
                    });
                }));
        }

        private async Task<Result<IEnumerable<Func<Task<Result<int>>>>>> GetRemovalFunctions(
            ClusterConfiguration clusterConfiguration, ClusterConfiguration.Node node, CancellationToken cancellationToken)
        {
            var partitionsApi = _bobApiClientFactory.GetPartitionsBobApiClient(node);
            var vdisksToCheck = clusterConfiguration.VDisks.Where(r => r.Replicas.Any(r => r.Node == node.Name));
            var partitionsResult = await _resultsCombiner.CollectResults(vdisksToCheck, async vd =>
            {
                Result<List<string>> partitionIdsResult = await partitionsApi.GetPartitions(vd, cancellationToken);
                return await partitionIdsResult.Bind(async partitionIds =>
                {
                    var partitionsResult = await _resultsCombiner.CollectResults<string, Partition>(partitionIds,
                        async p => await partitionsApi.GetPartition(vd.Id, p, cancellationToken));
                    return partitionsResult;
                });
            });
            return partitionsResult
                .Map(partitions => partitions
                    .GroupBy(p => (p.Timestamp, p.VDiskId))
                    .OrderBy(g => g.Key.Timestamp).ThenBy(g => g.Key.VDiskId)
                    .Select(g => CreateRemovalFunc(g.Key.VDiskId, g.Key.Timestamp, g.Count())));

            Func<Task<Result<int>>> CreateRemovalFunc(long vdiskId, long timestamp, int partitionsCount)
                => async () =>
                {
                    _logger.LogTrace("Removing partitions by timestamp {Timestamp} on {Node}/{VDisk}", timestamp, node.Name, vdiskId);
                    var result = await partitionsApi.DeletePartitionsByTimestamp(vdiskId, timestamp, cancellationToken);
                    return result.Map(r => r ? partitionsCount : 0);
                };
        }

        private async Task<Result<NodeStat>> CheckIfEnoughSpace(ClusterConfiguration.Node node, ByteSize threshold,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_arguments.DelayMilliseconds, cancellationToken);
            var spaceApi = _bobApiClientFactory.GetSpaceBobApiClient(node);
            Result<ulong> sizeResult = await spaceApi.GetFreeSpaceBytes(cancellationToken);
            return sizeResult.Map(d =>
            {
                var freeSpace = ByteSize.FromBytes(d);
                _logger.LogTrace("Current free space on {Node}: {Space}", node.Name, freeSpace);
                return new NodeStat(node, freeSpace, freeSpace > threshold);
            });
        }

        private readonly struct NodeStat
        {
            public NodeStat(ClusterConfiguration.Node node, ByteSize freeSpace,
                bool isEnoughSpace, ByteSize? startSpace = null, int removedPartitions = 0)
            {
                Node = node;
                StartSpace = startSpace ?? freeSpace;
                FreeSpace = freeSpace;
                IsEnoughSpace = isEnoughSpace;
                RemovedPartitions = removedPartitions;
            }

            public NodeStat WithStartSpace(ByteSize startSpace)
            {
                return new NodeStat(Node, FreeSpace, IsEnoughSpace, startSpace, RemovedPartitions);
            }

            public NodeStat WithRemovedPartitions(int count)
            {
                return new NodeStat(Node, FreeSpace, IsEnoughSpace, StartSpace, count);
            }

            public ClusterConfiguration.Node Node { get; }
            public ByteSize StartSpace { get; }
            public ByteSize FreeSpace { get; }
            public bool IsEnoughSpace { get; }
            public int RemovedPartitions { get; }
        }
    }
}