using System.Collections.Generic;
using System.Linq;
using BobAliensRecovery.AliensRecovery.Entities;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;

namespace BobAliensRecovery.AliensRecovery
{
    public class RecoveryTransactionsFinder
    {
        private readonly ILogger<RecoveryTransactionsFinder> _logger;

        public RecoveryTransactionsFinder(ILogger<RecoveryTransactionsFinder> logger)
        {
            _logger = logger;
        }

        internal IEnumerable<RecoveryTransaction> FindRecoveryTransactions(IDictionary<long, Replicas> replicasByVdiskId,
            IEnumerable<AlienDir> alienDirs)
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
                            _logger.LogError("Cannot find node in replicas for {dir}", sourceVdiskDir);
                    }
                    else
                        _logger.LogError("Cannot find recovery instructions for {dir}", sourceVdiskDir);
        }
    }
}