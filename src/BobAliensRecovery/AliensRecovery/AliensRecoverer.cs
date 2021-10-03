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
        private readonly RecoveryTransactionsFinder _recoveryTransactionsFinder;
        private readonly BlobsMover _blobsMover;
        private readonly NodesRestarter _nodesRestarter;

        public AliensRecoverer(ILogger<AliensRecoverer> logger,
            ReplicasFinder replicasFinder,
            AlienDirsFinder alienDirsFinder,
            RecoveryTransactionsFinder recoveryTransactionsFinder,
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
            ClusterOptions clusterOptions,
            AliensRecoveryOptions aliensRecoveryOptions,
            CancellationToken cancellationToken = default)
        {
            var replicas = _replicasFinder.FindReplicas(clusterConfiguration);
            _logger.LogInformation("Replicas found");

            var dirs = await _alienDirsFinder.FindAlienDirs(clusterConfiguration, clusterOptions, cancellationToken);
            _logger.LogInformation("Alien dirs found");

            var recoveryTransactions = _recoveryTransactionsFinder.FindRecoveryTransactions(replicas, dirs);
            _logger.LogInformation("Recovery transactions found");

            await _blobsMover.CopyBlobsAndDeleteClosed(recoveryTransactions, aliensRecoveryOptions,
                 cancellationToken);
            _logger.LogInformation("Blobs transfer finished");

            await _nodesRestarter.RestartTargetNodes(recoveryTransactions, clusterConfiguration,
                clusterOptions, cancellationToken);
            _logger.LogInformation("Nodes restarted");
        }
    }
}