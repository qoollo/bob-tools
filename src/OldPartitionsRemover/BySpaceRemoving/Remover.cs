using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
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
        private readonly RemovablePartitionsFinder _removablePartitionsFinder;
        private readonly ILogger<Remover> _logger;

        public Remover(Arguments arguments, IConfigurationFinder configurationFinder,
            IBobApiClientFactory bobApiClientFactory, ResultsCombiner resultsCombiner,
            RemovablePartitionsFinder removablePartitionsFinder, ILogger<Remover> logger)
        {
            _arguments = arguments;
            _configurationFinder = configurationFinder;
            _bobApiClientFactory = bobApiClientFactory;
            _resultsCombiner = resultsCombiner;
            _removablePartitionsFinder = removablePartitionsFinder;
            _logger = logger;
        }

        public async Task<Result<int>> RemovePartitionsBySpace(CancellationToken cancellationToken)
        {
            Result<ClusterConfiguration> configResult = await _configurationFinder.FindClusterConfiguration(
                    _arguments.ContinueOnError, cancellationToken);
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
            var thresholdTypeResult = _arguments.GetThresholdType();
            return await thresholdResult.Bind(bs => thresholdTypeResult.Bind(async tt =>
            {
                var spec = new ConditionSpecification(bs, tt);
                return await RemoveInClusterWithLessFreeSpace(clusterConfiguration, spec, cancellationToken);
            }));
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
                    _logger.LogInformation("Node {Node}, removed partitions: {Removed}, {Changed}",
                        n.Name, removed, nodeSpec.GetChangeString());
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
                    var isDoneRes = await nodeSpec.CheckIsDone(_logger, cancellationToken);
                    return await isDoneRes.Bind(async isDone =>
                    {
                        if (isDone)
                            return Result<int>.Ok(n);
                        var removeResult = await rem.Remove(cancellationToken);
                        return removeResult.Map(removed => n + (removed ? 1 : 0));
                    });
                }));
        }

        private async Task<Result<List<RemovablePartition>>> GetRemovalFunctions(
            ClusterConfiguration clusterConfiguration, ClusterConfiguration.Node node, CancellationToken cancellationToken)
        {
            var removableResult = await _removablePartitionsFinder.FindOnNode(clusterConfiguration, node, cancellationToken);
            return removableResult;
        }
    }
}
