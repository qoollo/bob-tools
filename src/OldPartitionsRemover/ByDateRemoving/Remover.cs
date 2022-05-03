using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
using BobToolsCli.Helpers;
using Microsoft.Extensions.Logging;
using OldPartitionsRemover.ByDateRemoving.Entities;
using OldPartitionsRemover.Entities;
using OldPartitionsRemover.Infrastructure;

namespace OldPartitionsRemover.ByDateRemoving
{
    public partial class Remover
    {
        private readonly Arguments _arguments;
        private readonly ILogger<Remover> _logger;
        private readonly IBobApiClientFactory _bobApiClientFactory;
        private readonly IConfigurationFinder _configurationFinder;
        private readonly ResultsCombiner _resultsCombiner;

        public Remover(Arguments arguments, ILogger<Remover> logger,
            IBobApiClientFactory bobApiClientFactory, IConfigurationFinder configurationFinder,
            ResultsCombiner resultsCombiner)
        {
            _arguments = arguments;
            _logger = logger;
            _bobApiClientFactory = bobApiClientFactory;
            _configurationFinder = configurationFinder;
            _resultsCombiner = resultsCombiner;
        }

        public async Task<Result<int>> RemoveOldPartitions(CancellationToken cancellationToken)
        {
            Result<ClusterConfiguration> configResult = await _configurationFinder.FindClusterConfiguration(cancellationToken);
            var thresholdResult = _arguments.GetThreshold();
            var removeOperations = await thresholdResult.Bind(t =>
            {
                _logger.LogInformation("Removing blos older than {Threshold}", t);
                return configResult.Bind(c => FindInCluster(c, t, cancellationToken));
            });
            return await removeOperations.Bind(InvokeOperations);
        }

        private async Task<Result<List<RemoveOperation>>> FindInCluster(ClusterConfiguration clusterConfig,
            DateTime threshold, CancellationToken cancellationToken)
            => await _resultsCombiner.CollectResults(clusterConfig.Nodes,
                async node => await FindOnNode(clusterConfig, node, threshold, cancellationToken));

        private async Task<Result<List<RemoveOperation>>> FindOnNode(ClusterConfiguration clusterConfig,
            ClusterConfiguration.Node node, DateTime threshold, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Preparing partitions to remove from node {Node}", node.Name);

            var vdisksOnNode = clusterConfig.VDisks.Where(vd => vd.Replicas.Any(r => r.Node == node.Name));
            var nodeApi = new NodeApi(_bobApiClientFactory.GetPartitionsBobApiClient(node), cancellationToken);

            return await _resultsCombiner.CollectResults(vdisksOnNode, async vdisk => await FindOnVDisk(vdisk, nodeApi, threshold));
        }

        private async Task<Result<List<RemoveOperation>>> FindOnVDisk(ClusterConfiguration.VDisk vdisk, NodeApi nodeApi,
            DateTime threshold)
        {
            _logger.LogDebug("Preparing partitions to remove from vdisk {VDisk}", vdisk.Id);

            var partitionFunctions = new PartitionFunctions(vdisk, nodeApi);

            var partitionIdsResult = await partitionFunctions.FindPartitionIds();
            return await partitionIdsResult.Bind(partitionIds => FindWithinPatitionIds(partitionIds, partitionFunctions, threshold));
        }

        private async Task<Result<List<RemoveOperation>>> FindWithinPatitionIds(List<string> partitionIds,
            PartitionFunctions partitionFunctions, DateTime threshold)
        {
            var partitionsResult = await GetPartitions(partitionIds, partitionFunctions);
            return partitionsResult.Bind(partitions => FindWithinPartitions(partitions, partitionFunctions, threshold));
        }

        private async Task<Result<List<Partition>>> GetPartitions(List<string> partitionIds,
            PartitionFunctions partitionFunctions)
        {
            return await _resultsCombiner.CollectResults(partitionIds, async p => await partitionFunctions.FindPartitionById(p));
        }

        private Result<List<RemoveOperation>> FindWithinPartitions(List<Partition> partitionInfos,
            PartitionFunctions partitionFunctions, DateTime threshold)
        {
            return FindByTimestamp(partitionInfos, partitionFunctions, threshold);
        }

        private Result<List<RemoveOperation>> FindByTimestamp(List<Partition> partitionInfos,
            PartitionFunctions partitionFunctions, DateTimeOffset threshold)
        {
            var oldTimestamps = partitionInfos.Select(p => p.Timestamp)
                .Where(p => DateTimeOffset.FromUnixTimeSeconds(p) < threshold)
                .Distinct()
                .ToArray();
            var countByOldTimestamp = partitionInfos.Select(p => p.Timestamp)
                .Where(p => DateTimeOffset.FromUnixTimeSeconds(p) < threshold)
                .GroupBy(p => p)
                .ToDictionary(g => g.Key, g => g.Count());
            if (oldTimestamps.Length > 0)
                _logger.LogInformation("Preparing partitions from {TimestampsCount} timestamps to remove", oldTimestamps.Length);
            else
                _logger.LogInformation("No partitions to be removed");

            var removeOperations = oldTimestamps.Select<long, RemoveOperation>(ts =>
                async () => (await partitionFunctions.RemovePartitionsByTimestamp(ts)).Map(t => t ? countByOldTimestamp[ts] : 0)).ToList();
            return Result<List<RemoveOperation>>.Ok(removeOperations);
        }

        private async Task<Result<int>> InvokeOperations(List<RemoveOperation> ops)
        {
            _logger.LogInformation("Invoking {RemoveOperationsCount} remove operations", ops.Count);
            return await _resultsCombiner.CombineResults(ops, 0, async (c, n) => (await n()).Map(r => c + r));
        }
    }
}