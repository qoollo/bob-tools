using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobAliensRecovery.Exceptions;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;
using RemoteFileCopy.FilesFinding;

namespace BobAliensRecovery.AliensRecovery
{
    public class BlobsMover
    {
        private readonly RemoteFileCopier _remoteFileCopier;
        private readonly PartitionInfoAggregator _partitionInfoAggregator;
        private readonly FilesFinder _filesFinder;
        private readonly ILogger<BlobsMover> _logger;

        public BlobsMover(RemoteFileCopier remoteFileCopier,
            PartitionInfoAggregator partitionInfoAggregator,
            FilesFinder filesFinder,
            ILogger<BlobsMover> logger)
        {
            _remoteFileCopier = remoteFileCopier;
            _partitionInfoAggregator = partitionInfoAggregator;
            _filesFinder = filesFinder;
            _logger = logger;
        }


        internal async Task CopyBlobsAndDeleteClosed(
            IEnumerable<RecoveryTransaction> recoveryTransactions,
            AliensRecoveryOptions aliensRecoveryOptions,
            CancellationToken cancellationToken)
        {
            var blobsToRemove = await CopyBlobs(recoveryTransactions, aliensRecoveryOptions, cancellationToken);
            _logger.LogInformation("Copied {blobsCount} blobs", blobsToRemove.Count);

            if (aliensRecoveryOptions.RemoveCopied)
            {
                await RemoveAlreadyMovedFiles(recoveryTransactions, cancellationToken);
            }
        }

        private async Task<List<BlobInfo>> CopyBlobs(IEnumerable<RecoveryTransaction> recoveryTransactions,
            AliensRecoveryOptions aliensRecoveryOptions, CancellationToken cancellationToken)
        {
            var blobsToRemove = new List<BlobInfo>();
            foreach (var transaction in recoveryTransactions)
            {
                var rsyncResult = await _remoteFileCopier.CopyWithRsync(transaction.From, transaction.To, cancellationToken);
                if (!rsyncResult.StdErr.Any())
                {
                    _logger.LogDebug("Synced {transaction}", transaction.ToString());
                    var partitions = _partitionInfoAggregator.GetPartitionInfos(rsyncResult.SyncedFiles);

                    foreach (var partition in partitions)
                        blobsToRemove.AddRange(partition.Blobs.Where(b => b.IsClosed));
                }
                else
                {
                    _logger.LogError("Sync {transaction} failed: {stderr}", transaction);
                    _logger.LogDebug("Sync {transaction} err: {stderr}", transaction,
                        string.Join(Environment.NewLine, rsyncResult.StdErr));

                    if (!aliensRecoveryOptions.ContinueOnError)
                        throw new OperationException($"Recovery transaction {transaction} failed");
                }
            }

            return blobsToRemove;
        }

        private async Task RemoveAlreadyMovedFiles(IEnumerable<RecoveryTransaction> transactions,
            CancellationToken cancellationToken = default)
        {
            foreach (var transaction in transactions)
            {
                var srcFiles = await _filesFinder.FindFiles(transaction.From, cancellationToken);
                var dstFiles = await _filesFinder.FindFiles(transaction.To, cancellationToken);

                var equal = srcFiles.SelectMany(s => dstFiles.Select(d => (s, d)))
                    .Where(t => AreEqual(transaction!.From, transaction!.To, t.s, t.d));

                var filesToRemove = equal.Select(t => t.s);

                if (await _remoteFileCopier.RemoveFiles(filesToRemove, cancellationToken))
                    _logger.LogDebug("Successfully removed source files from {dir}", transaction.From);
                else
                    _logger.LogWarning("Failed to remove source files from {dir}", transaction.From);

                if (await _remoteFileCopier.RemoveEmptySubdirs(transaction.From, cancellationToken))
                    _logger.LogDebug("Successfully removed empty directories at {dir}", transaction.From);
                else
                    _logger.LogWarning("Failed to clean up empty directories at {dir}", transaction.From);
            }
        }

        private static bool AreEqual(RemoteDir from, RemoteDir to, RemoteFileInfo fromFile, RemoteFileInfo toFile)
        {
            return fromFile.Checksum == toFile.Checksum
                && fromFile.LengthBytes == toFile.LengthBytes
                && fromFile.Filename.Replace(from.Path, "") == toFile.Filename.Replace(to.Path, "");
        }
    }
}