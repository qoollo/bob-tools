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
            return nodeStats.Map(rs =>
            {
                var removed = 0;
                foreach (var r in rs)
                {
                    removed += r;
                }
                return removed;
            });
        }

        private async Task<Result<List<int>>> RemoveInCluster(ClusterConfiguration clusterConfiguration,
            CancellationToken cancellationToken)
        {
            var thresholdResult = _arguments.GetThreshold();
            return await thresholdResult.Bind(async bs =>
            {
                var spec = new ConditionSpecification(bs);
                return await RemoveInClusterWithLessFreeSpace(clusterConfiguration, spec, cancellationToken);
            });
        }

        private async Task<Result<List<int>>> RemoveInClusterWithLessFreeSpace(ClusterConfiguration clusterConfiguration,
            ConditionSpecification conditionSpecification, CancellationToken cancellationToken)
        {
            return await _resultsCombiner.CollectResults(clusterConfiguration.Nodes, async n =>
            {
                var nodeSpec = conditionSpecification.GetForNode(_bobApiClientFactory, n);
                var removeResult = await RemoveOnNode(clusterConfiguration, nodeSpec, cancellationToken);
                return removeResult.Map(removed =>
                {
                    _logger.LogInformation("Node {Node}, removed partitions: {Removed}, freed: {Freed}",
                        n.Name, removed, nodeSpec.GetSpaceStat());
                    return removed;
                });
            });
        }

        private async Task<Result<int>> RemoveOnNode(ClusterConfiguration clusterConfiguration,
            NodeConditionSpecification nodeSpec, CancellationToken cancellationToken)
        {
            var removalFunctionsResult = await GetRemovalFunctions(clusterConfiguration, nodeSpec.Node, cancellationToken);
            return await removalFunctionsResult
                .Bind(async removalFunctions => await _resultsCombiner.CombineResults(removalFunctions, 0, async (n, rem) =>
                {
                    var isDoneRes = await nodeSpec.CheckIsDone(cancellationToken);
                    return await isDoneRes.Bind(async isDone =>
                    {
                        if (isDone)
                            return Result<int>.Ok(n);
                        var removeResult = await rem();
                        return removeResult.Map(removed => n + removed);
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
    }
}