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

        public async Task<Result<bool>> RemoveOldPartitions(CancellationToken cancellationToken)
        {
            Result<ClusterConfiguration> configResult = await _configurationFinder.FindClusterConfiguration(cancellationToken);
            var removeOperations = await configResult.Bind(c => FindInCluster(c, cancellationToken));
            return await removeOperations.Bind(InvokeOperations);
        }

        private async Task<Result<List<RemoveOperation>>> FindInCluster(ClusterConfiguration clusterConfig,
            CancellationToken cancellationToken)
            => await _resultsCombiner.CollectResults(clusterConfig.Nodes, async node => await FindOnNode(clusterConfig, node, cancellationToken));

        private async Task<Result<List<RemoveOperation>>> FindOnNode(ClusterConfiguration clusterConfig,
            ClusterConfiguration.Node node, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Removing partitions on node {Node}", node.Name);

            var vdisksOnNode = clusterConfig.VDisks.Where(vd => vd.Replicas.Any(r => r.Node == node.Name));
            var nodeApi = new NodeApi(_bobApiClientFactory.GetPartitionsBobApiClient(node), cancellationToken);

            return await _resultsCombiner.CollectResults(vdisksOnNode, async vdisk => await FindOnVDisk(vdisk, nodeApi));
        }

        private async Task<Result<List<RemoveOperation>>> FindOnVDisk(ClusterConfiguration.VDisk vdisk, NodeApi nodeApi)
        {
            _logger.LogDebug("Removing partitions on vdisk {VDisk}", vdisk.Id);

            var partitionFunctions = new PartitionFunctions(vdisk, nodeApi);

            var partitionIdsResult = await partitionFunctions.FindPartitionIds();
            return await partitionIdsResult.Bind(partitionIds => FindWithinPatitionIds(partitionIds, partitionFunctions));
        }

        private async Task<Result<List<RemoveOperation>>> FindWithinPatitionIds(List<string> partitionIds, PartitionFunctions partitionFunctions)
        {
            var partitionsResult = await GetPartitions(partitionIds, partitionFunctions.FindPartitionById);
            return partitionsResult.Bind(partitions => FindWithinPartitions(partitions, partitionFunctions.RemovePartitionsByTimestamp));
        }

        private async Task<Result<List<Partition>>> GetPartitions(List<string> partitionIds,
            PartitionFunctions.PartitionFinder find)
        {
            return await _resultsCombiner.CollectResults(partitionIds, async p => await find(p));
        }

        private Result<List<RemoveOperation>> FindWithinPartitions(List<Partition> partitionInfos,
            PartitionFunctions.PartitionsRemover remove)
        {
            var thresholdResult = _arguments.GetThreshold();
            return thresholdResult.Bind(threshold => FindByTimestamp(partitionInfos, remove, threshold));
        }

        private Result<List<RemoveOperation>> FindByTimestamp(List<Partition> partitionInfos,
            PartitionFunctions.PartitionsRemover remove, DateTimeOffset threshold)
        {
            var oldTimestamps = partitionInfos.Select(p => p.Timestamp)
                .Where(p => DateTimeOffset.FromUnixTimeSeconds(p) < threshold)
                .Distinct()
                .ToArray();
            if (oldTimestamps.Length > 0)
                _logger.LogInformation("Removing partitions for {TimestampsCount} timestamps", oldTimestamps.Length);
            else
                _logger.LogInformation("No partitions to be removed");

            var removeOperations = oldTimestamps.Select(ts => new RemoveOperation(() => remove(ts))).ToList();
            return Result<List<RemoveOperation>>.Ok(removeOperations);
        }

        private async Task<Result<bool>> InvokeOperations(List<RemoveOperation> ops)
        {
            _logger.LogInformation("Invoking {RemoveOperationsCount} remove operations", ops.Count);
            return await _resultsCombiner.CombineResults(ops, true, (_, n) => n.Func());
        }
    }
}