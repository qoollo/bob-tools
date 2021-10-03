using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;

namespace BobAliensRecovery.AliensRecovery
{
    public class BlobsMover
    {
        private readonly RemoteFileCopier _remoteFileCopier;
        private readonly PartitionInfoAggregator _partitionInfoAggregator;
        private readonly ILogger<BlobsMover> _logger;

        public BlobsMover(RemoteFileCopier remoteFileCopier,
            PartitionInfoAggregator partitionInfoAggregator,
            ILogger<BlobsMover> logger)
        {
            _remoteFileCopier = remoteFileCopier;
            _partitionInfoAggregator = partitionInfoAggregator;
            _logger = logger;
        }


        internal async Task CopyBlobsAndDeleteClosed(
            IEnumerable<RecoveryTransaction> recoveryTransactions,
            AliensRecoveryOptions aliensRecoveryOptions,
            CancellationToken cancellationToken)
        {
            var blobsToRemove = await CopyBlobs(recoveryTransactions, cancellationToken);

            if (aliensRecoveryOptions.RemoveSource)
            {
                await RemoveBlobs(blobsToRemove, cancellationToken);
                await RemoveEmptyDirectories(recoveryTransactions, cancellationToken);
            }
        }

        private async Task<List<BlobInfo>> CopyBlobs(IEnumerable<RecoveryTransaction> recoveryTransactions, CancellationToken cancellationToken)
        {
            var blobsToRemove = new List<BlobInfo>();
            foreach (var transaction in recoveryTransactions)
            {
                try
                {
                    var rsyncResult = await _remoteFileCopier.CopyWithRsync(transaction.From, transaction.To, cancellationToken);
                    if (!rsyncResult.StdErr.Any())
                    {
                        _logger.LogDebug("Synced {transaction}", transaction.ToString());
                        var partitions = _partitionInfoAggregator.GetPartitionInfos(rsyncResult.SyncedFiles);

                        foreach (var partition in partitions)
                        {
                            blobsToRemove.AddRange(partition.Blobs.Where(b => b.IsClosed));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Sync {transaction} failed: {stderr}", transaction.ToString(),
                            string.Join(Environment.NewLine, rsyncResult.StdErr));
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to perform recovery {transaction}", transaction.ToString());
                }
            }

            return blobsToRemove;
        }

        private async Task RemoveBlobs(List<BlobInfo> blobsToRemove, CancellationToken cancellationToken)
        {
            foreach (var blob in blobsToRemove)
            {
                if (await _remoteFileCopier.RemoveFiles(blob.Files, cancellationToken))
                    _logger.LogDebug("Removed {blob}", blob.ToString());
                else
                    _logger.LogWarning("Error while removing blob {blob}", blob.ToString());
            }
        }

        private async Task RemoveEmptyDirectories(IEnumerable<RecoveryTransaction> recoveryTransactions, CancellationToken cancellationToken)
        {
            foreach (var recoveryTransaction in recoveryTransactions)
            {
                if (await _remoteFileCopier.RemoveEmptySubdirs(recoveryTransaction.From, cancellationToken))
                    _logger.LogDebug("Successfully removed empty directories at {target}", recoveryTransaction.From.ToString());
                else
                    _logger.LogWarning("Failed to clean up empty directories at {target}", recoveryTransaction.From.ToString());
            }
        }
    }
}