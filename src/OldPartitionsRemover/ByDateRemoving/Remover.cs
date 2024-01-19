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
        private readonly RemovablePartitionsFinder _removablePartitionsFinder;

        public Remover(Arguments arguments, ILogger<Remover> logger,
            IBobApiClientFactory bobApiClientFactory, IConfigurationFinder configurationFinder,
            ResultsCombiner resultsCombiner, RemovablePartitionsFinder removablePartitionsFinder)
        {
            _arguments = arguments;
            _logger = logger;
            _bobApiClientFactory = bobApiClientFactory;
            _configurationFinder = configurationFinder;
            _resultsCombiner = resultsCombiner;
            _removablePartitionsFinder = removablePartitionsFinder;
        }

        public async Task<Result<int>> RemoveOldPartitions(CancellationToken cancellationToken)
        {
            Result<ClusterConfiguration> configResult = await _configurationFinder.FindClusterConfiguration(
                    _arguments.ContinueOnError, cancellationToken);
            var thresholdResult = _arguments.GetThreshold();
            var removeOperations = await thresholdResult.Bind(t =>
            {
                _logger.LogInformation("Removing blobs older than {Threshold}", t);
                return configResult.Bind(c => FindInCluster(c, t, cancellationToken));
            });
            return await removeOperations.Bind(async ops => await InvokeOperations(ops, cancellationToken));
        }

        private async Task<Result<List<RemovablePartition>>> FindInCluster(ClusterConfiguration clusterConfig,
            DateTime threshold, CancellationToken cancellationToken)
        {
            return (await _removablePartitionsFinder.Find(clusterConfig, _arguments.AllowAlien, cancellationToken))
                    .Map(rms => {
                        rms.RemoveAll(rm => rm.Timestamp >= threshold);
                        return rms;
                    });
        }

        private async Task<Result<int>> InvokeOperations(List<RemovablePartition> ops, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Invoking {RemoveOperationsCount} remove operations", ops.Count);
            return await _resultsCombiner.CombineResults(ops, 0, async (c, n) => (await n.Remove(cancellationToken)).Map(r => c + (r ? 1 : 0)));
        }
    }
}
