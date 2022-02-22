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
            var vdisksToCheck = clusterConfiguration.VDisks.Where(r => r.Replicas.Any(r => r.Node == node.Name));
            return await _resultsCombiner.CombineResults(vdisksToCheck, false, async (enoughSpaceOnNode, vd) =>
            {
                if (enoughSpaceOnNode)
                    return Result<bool>.Ok(enoughSpaceOnNode);

                _logger.LogDebug("Removing partitions on vdisk {VDisk} on node {Node}", vd.Id, node.Name);

                var partitionsApi = _bobApiClientFactory.GetPartitionsBobApiClient(node);
                return await RemoveOnVDiskUntilEnoughSpace(vd, CheckIfEnoughSpace, partitionsApi, cancellationToken);
            });

            async Task<Result<bool>> CheckIfEnoughSpace()
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

        private async Task<Result<bool>> RemoveOnVDiskUntilEnoughSpace(ClusterConfiguration.VDisk vd, Func<Task<Result<bool>>> checkIfEnoughSpace,
            IPartitionsBobApiClient partitionsApi, CancellationToken cancellationToken)
        {
            Result<List<string>> partitionIdsResult = await partitionsApi.GetPartitions(vd, cancellationToken);
            return await partitionIdsResult.Bind(async partitionIds =>
            {
                var partitionsResult = await _resultsCombiner.CollectResults<string, Partition>(partitionIds,
                    async p => await partitionsApi.GetPartition(vd.Id, p, cancellationToken));
                return await partitionsResult.Bind(async partitions =>
                {
                    var timestamps = partitions.Select(p => p.Timestamp).Distinct().OrderBy(_ => _);
                    return await _resultsCombiner.CombineResults(timestamps, !timestamps.Any(), async (enoughSpaceOnVdisk, ts) =>
                    {
                        if (enoughSpaceOnVdisk)
                            return Result<bool>.Ok(enoughSpaceOnVdisk);

                        _logger.LogTrace("Sending delete request for {VDisk}/{Timestamp}", vd.Id, ts);
                        Result<bool> deleteResult = await partitionsApi.DeletePartitionsByTimestamp(vd.Id, ts, cancellationToken);
                        return await deleteResult.Bind(async _ => await checkIfEnoughSpace());
                    });
                });
            });
        }
    }
}