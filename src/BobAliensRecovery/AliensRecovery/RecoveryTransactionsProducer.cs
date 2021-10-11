using System.Collections.Generic;
using System.Linq;
using BobAliensRecovery.AliensRecovery.Entities;
using BobAliensRecovery.Exceptions;
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

        internal IEnumerable<RecoveryTransaction> ProduceRecoveryTransactions(IDictionary<long, Replicas> replicasByVdiskId,
            AliensRecoveryOptions aliensRecoveryOptions, IEnumerable<AlienDir> alienDirs)
        {
            // We check all disks as aliens are saved on any of them
            foreach (var alienSourceNode in alienDirs.SelectMany(_ => _.Children))
                foreach (var sourceVdiskDir in alienSourceNode.Children)
                    if (long.TryParse(sourceVdiskDir.DirName, out var id)
                        && replicasByVdiskId.TryGetValue(id, out var rs))
                    {
                        var sourceRemote = new RemoteDir(sourceVdiskDir.Node.GetIPAddress(), sourceVdiskDir.Directory.Path);
                        var targetNodeName = alienSourceNode.DirName;
                        var targetRemote = rs.FindRemoteDirectory(targetNodeName);

                        if (targetRemote != null)
                            yield return new RecoveryTransaction(sourceRemote, targetRemote, targetNodeName, rs);
                        else
                            aliensRecoveryOptions.LogError<ConfigurationException>(_logger,
                                "Cannot find node in replicas for {sourceVdiskDir}", sourceVdiskDir);
                    }
                    else
                    {
                        throw new ConfigurationException($"Cannot find recovery instructions for {sourceVdiskDir}");
                    }
        }
    }
}