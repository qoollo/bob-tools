using System;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
using ByteSizeLib;
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

        public Remover(Arguments arguments, IConfigurationFinder configurationFinder,
            IBobApiClientFactory bobApiClientFactory, ResultsCombiner resultsCombiner)
        {
            _arguments = arguments;
            _configurationFinder = configurationFinder;
            _bobApiClientFactory = bobApiClientFactory;
            _resultsCombiner = resultsCombiner;
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
            return await _resultsCombiner.CombineResults(clusterConfiguration.Nodes, true, async (p, n) =>
            {
                var spaceClient = _bobApiClientFactory.GetSpaceBobApiClient(n);
                Result<ulong> space = await spaceClient.GetFreeSpaceBytes(cancellationToken);
                return space.Map(d => p && d > threshold.Bytes);
            });
        }
    }
}