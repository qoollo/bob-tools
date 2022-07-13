using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli.Helpers;
using BobToolsCli.Exceptions;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;

namespace BobAliensRecovery.AliensRecovery
{
    public class AliensRecoverer
    {
        private readonly ILogger<AliensRecoverer> _logger;
        private readonly ReplicasFinder _replicasFinder;
        private readonly AlienDirsFinder _alienDirsFinder;
        private readonly RecoveryTransactionsProducer _recoveryTransactionsFinder;
        private readonly BlobsMover _blobsMover;
        private readonly NodesRestarter _nodesRestarter;

        public AliensRecoverer(ILogger<AliensRecoverer> logger,
            ReplicasFinder replicasFinder,
            AlienDirsFinder alienDirsFinder,
            RecoveryTransactionsProducer recoveryTransactionsFinder,
            BlobsMover blobsMover,
            NodesRestarter nodesRestarter)
        {
            _logger = logger;
            _replicasFinder = replicasFinder;
            _alienDirsFinder = alienDirsFinder;
            _recoveryTransactionsFinder = recoveryTransactionsFinder;
            _blobsMover = blobsMover;
            _nodesRestarter = nodesRestarter;
        }

        internal async Task RecoverAliens(
            ClusterConfiguration clusterConfiguration,
            BobApiClientProvider bobApiClientProvider,
            AliensRecoveryOptions aliensRecoveryOptions,
            CancellationToken cancellationToken = default)
        {
            var unavailableNodes = await GetUnavailableNodes(clusterConfiguration, bobApiClientProvider, cancellationToken);
            if (unavailableNodes.Count > 0)
                aliensRecoveryOptions.LogErrorWithPossibleException<ClusterStateException>(_logger,
                    "The following nodes are not available: {nodes}", string.Join(", ", unavailableNodes));

            var replicas = await _replicasFinder.FindReplicasByVdiskId(clusterConfiguration,
                bobApiClientProvider, aliensRecoveryOptions, cancellationToken);
            _logger.LogInformation("Replicas found");

            var dirs = await _alienDirsFinder.FindAlienDirs(clusterConfiguration, bobApiClientProvider,
                aliensRecoveryOptions, cancellationToken);
            _logger.LogInformation("Alien dirs found");

            var recoveryTransactions = await _recoveryTransactionsFinder.ProduceRecoveryTransactions(replicas,
                aliensRecoveryOptions, dirs);
            _logger.LogInformation("Recovery transactions found");

            await _blobsMover.CopyBlobsAndDeleteClosed(recoveryTransactions, aliensRecoveryOptions,
                 cancellationToken);
            _logger.LogInformation("Blobs transfer finished");

            if (aliensRecoveryOptions.RestartNodes)
            {
                await _nodesRestarter.RestartTargetNodes(recoveryTransactions, clusterConfiguration,
                    bobApiClientProvider, aliensRecoveryOptions, cancellationToken);
                _logger.LogInformation("Nodes restarted");
            }
        }

        private async Task<List<string>> GetUnavailableNodes(
            ClusterConfiguration clusterConfiguration,
            BobApiClientProvider bobApiClientProvider,
            CancellationToken cancellationToken)
        {
            var result = new List<string>();
            foreach (var node in clusterConfiguration.Nodes)
            {
                using var client = bobApiClientProvider.GetClient(node);
                var res = await client.GetStatus(cancellationToken);
                if (res.TryGetError(out var _))
                    result.Add(node.Name);
            }
            return result;
        }
    }
}
