using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobToolsCli.Exceptions;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;

namespace BobAliensRecovery.AliensRecovery
{
    public class RecoveryTransactionsProducer
    {
        private readonly ILogger<RecoveryTransactionsProducer> _logger;

        public RecoveryTransactionsProducer(ILogger<RecoveryTransactionsProducer> logger)
        {
            _logger = logger;
        }

        internal async Task<List<RecoveryTransaction>> ProduceRecoveryTransactions(
            IReadOnlyDictionary<long, Replicas> replicasByVdiskId, AliensRecoveryOptions aliensRecoveryOptions,
            IEnumerable<AlienDir> alienDirs)
        {
            var result = new List<RecoveryTransaction>();

            // We check all disks as aliens are saved on any of them
            foreach (var alienSourceNode in alienDirs.SelectMany(_ => _.Children))
                foreach (var sourceVdiskDir in alienSourceNode.Children)
                    if (long.TryParse(sourceVdiskDir.DirName, out var id)
                        && replicasByVdiskId.TryGetValue(id, out var rs))
                    {
                        var addr = await sourceVdiskDir.Node.FindIPAddress();
                        if (addr != null)
                        {
                            var sourceRemote = new RemoteDir(addr, sourceVdiskDir.Directory.Path);
                            var targetNodeName = alienSourceNode.DirName;
                            var targetRemote = rs.FindRemoteDirectory(targetNodeName);

                            if (targetRemote != null)
                                result.Add(new RecoveryTransaction(sourceRemote, targetRemote, targetNodeName, rs));
                            else
                                aliensRecoveryOptions.LogErrorWithPossibleException<ConfigurationException>(_logger,
                                    "Cannot find node in replicas for {sourceVdiskDir}", sourceVdiskDir);
                        }
                        else
                        {
                            aliensRecoveryOptions.LogErrorWithPossibleException<OperationException>(_logger,
                                "Failed to find ip address of {node}", sourceVdiskDir.Node);
                        }
                    }
                    else
                    {
                        throw new ConfigurationException($"Cannot find recovery instructions for {sourceVdiskDir}");
                    }

            return result;
        }
    }
}