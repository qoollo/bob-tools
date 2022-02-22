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

        public async Task<Result<bool>> RemovePartitionsBySpace(CancellationToken cancellationToken)
        {
            Result<ClusterConfiguration> configResult = await _configurationFinder.FindClusterConfiguration(cancellationToken);
            return await configResult.Bind(async conf => await RemoveInCluster(conf, cancellationToken));
        }

        private async Task<Result<bool>> RemoveInCluster(ClusterConfiguration clusterConfiguration, CancellationToken cancellationToken)
        {
            var thresholdResult = _arguments.GetThreshold();
            return await thresholdResult.Bind(async bs =>
            {
                return await RemoveInClusterWithLessFreeSpace(clusterConfiguration, bs, cancellationToken);
            });
        }

        private async Task<Result<bool>> RemoveInClusterWithLessFreeSpace(ClusterConfiguration clusterConfiguration, ByteSize threshold,
            CancellationToken cancellationToken)
        {
            return await _resultsCombiner.CombineResults(clusterConfiguration.Nodes, true, async (_, n) =>
            {
                var spaceClient = _bobApiClientFactory.GetSpaceBobApiClient(n);
                Result<ulong> spaceResult = await spaceClient.GetFreeSpaceBytes(cancellationToken);
                return await spaceResult.Map(d => ByteSize.FromBytes(d)).Bind(async space =>
                {
                    _logger.LogInformation("Free space on {Node}: {FreeSpace}", n.Name, space);
                    if (space > threshold)
                        return Result<bool>.Ok(true);

                    _logger.LogInformation("Not enough space on node {Node} ({Actual} < {Threshold})",
                        n.Name, space, threshold);
                    return await RemoveOnNode(clusterConfiguration, n, threshold, cancellationToken);
                });
            });
        }

        private async Task<Result<bool>> RemoveOnNode(ClusterConfiguration clusterConfiguration, ClusterConfiguration.Node node, ByteSize threshold,
            CancellationToken cancellationToken)
        {
            var removalFunctionsResult = await GetRemovalFunctions(clusterConfiguration, node, cancellationToken);
            return await removalFunctionsResult
                .Bind(async removalFunctions => await _resultsCombiner.CombineResults(removalFunctions, false, async (enoughSpace, f) =>
                {
                    if (enoughSpace)
                        return Result<bool>.Ok(enoughSpace);
                    var deleteResult = await f();
                    return await deleteResult.Bind(async _ => await CheckIfEnoughSpace(node, threshold, cancellationToken));
                }));
        }

        private async Task<Result<IEnumerable<Func<Task<Result<bool>>>>>> GetRemovalFunctions(
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
                    .OrderBy(p => p.Timestamp).ThenBy(p => p.VDiskId)
                    .Select(p => CreateRemovalFunc(p.VDiskId, p.Timestamp)));

            Func<Task<Result<bool>>> CreateRemovalFunc(long vdiskId, long timestamp)
                => async () =>
                {
                    _logger.LogTrace("Removing partitions by timestamp {Timestamp} on {Node}/{VDisk}", timestamp, node.Name, vdiskId);
                    return await partitionsApi.DeletePartitionsByTimestamp(vdiskId, timestamp, cancellationToken);
                };
        }

        private async Task<Result<bool>> CheckIfEnoughSpace(ClusterConfiguration.Node node, ByteSize threshold, CancellationToken cancellationToken)
        {
            await Task.Delay(_arguments.DelaySeconds * 1000, cancellationToken);
            var spaceApi = _bobApiClientFactory.GetSpaceBobApiClient(node);
            Result<ulong> sizeResult = await spaceApi.GetFreeSpaceBytes(cancellationToken);
            return sizeResult.Map(d =>
            {
                var freeSpace = ByteSize.FromBytes(d);
                _logger.LogTrace("Current free space on {Node}: {Space}", node.Name, freeSpace);
                return freeSpace > threshold;
            });
        }
    }
}