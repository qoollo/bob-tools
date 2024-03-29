using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobApi;
using BobApi.BobEntities;
using BobToolsCli.Helpers;
using BobToolsCli.Exceptions;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery.AliensRecovery
{
    public class NodesRestarter
    {
        private readonly ILogger<NodesRestarter> _logger;

        public NodesRestarter(ILogger<NodesRestarter> logger)
        {
            _logger = logger;
        }

        internal async Task RestartTargetNodes(IEnumerable<RecoveryTransaction> recoveryTransactions,
            ClusterConfiguration clusterConfiguration, BobApiClientProvider bobApiClientProvider,
            AliensRecoveryOptions aliensRecoveryOptions, CancellationToken cancellationToken)
        {
            var restartOperations = GetRestartOperations(recoveryTransactions, clusterConfiguration);

            foreach (var ro in restartOperations.Distinct())
            {
                using var api = bobApiClientProvider.GetClient(ro.Node);
                var restartResult = await api.RestartDisk(ro.DiskName, cancellationToken);
                if (restartResult.TryGetData(out var isRestarted) && isRestarted)
                    _logger.LogDebug("Reloaded {disk} on {node}", ro.DiskName, ro.Node.Name);
                else
                    aliensRecoveryOptions.LogErrorWithPossibleException<ClusterStateException>(
                        _logger, "Failed to restart disks on node {node}", ro.Node.Name);
            }
        }


        private IEnumerable<RestartOperation> GetRestartOperations(IEnumerable<RecoveryTransaction> recoveryTransactions,
            ClusterConfiguration clusterConfiguration)
        {
            foreach (var t in recoveryTransactions)
            {
                var targetDisk = clusterConfiguration.FindDiskName(t.TargetNodeName, t.Source.VDiskId);

                var node = clusterConfiguration.Nodes.Find(n => n.Name == t.TargetNodeName)!;

                yield return new RestartOperation(node, targetDisk);
            }
        }
    }
}
