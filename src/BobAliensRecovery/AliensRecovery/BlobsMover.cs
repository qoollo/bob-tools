using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobToolsCli.Exceptions;
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
            _logger.LogInformation("{count} transactions to invoke, {degree} parallel requests", recoveryTransactions.Count(),
                aliensRecoveryOptions.CopyParallelDegree);
            var blobsToRemove = await CopyBlobsInParallel(recoveryTransactions, aliensRecoveryOptions, cancellationToken);
            _logger.LogInformation("Copied {blobsCount} blobs", blobsToRemove.Count);

            if (aliensRecoveryOptions.RemoveCopied)
            {
                await RemoveAlreadyMovedFiles(recoveryTransactions, cancellationToken);
            }
        }

        private async Task<List<BlobInfo>> CopyBlobsInParallel(IEnumerable<RecoveryTransaction> recoveryTransactions,
            AliensRecoveryOptions aliensRecoveryOptions, CancellationToken cancellationToken)
        {
            var remaining = recoveryTransactions.ToList();
            var countByAddress = remaining.SelectMany(t => new[] { t.From.Address, t.To.Address }).Distinct()
                .ToDictionary(a => a, _ => 0);

            var results = new List<BlobInfo>[remaining.Count];
            await Parallel.ForEachAsync(Enumerable.Range(0, remaining.Count),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = aliensRecoveryOptions.CopyParallelDegree
                },
                async (ind, t) =>
                {
                    var transaction = TakeTransaction(remaining, countByAddress);
                    try
                    {
                        results[ind] = await InvokeTransaction(transaction, aliensRecoveryOptions, t);
                    }
                    finally
                    {
                        CleanUp(transaction, countByAddress);
                    }
                });
            return results.Where(r => r != null).SelectMany(l => l).ToList();
        }

        private static RecoveryTransaction TakeTransaction(List<RecoveryTransaction> remaining, Dictionary<IPAddress, int> countByAddress)
        {
            RecoveryTransaction transaction;
            lock (countByAddress)
            {
                transaction = remaining.OrderBy(t => countByAddress[t.From.Address] + countByAddress[t.To.Address]).First();
                remaining.Remove(transaction);
                countByAddress[transaction.From.Address]++;
                countByAddress[transaction.To.Address]++;
            }

            return transaction;
        }

        private static void CleanUp(RecoveryTransaction transaction, Dictionary<IPAddress, int> countByAddress)
        {
            lock (countByAddress)
            {
                countByAddress[transaction.From.Address]--;
                countByAddress[transaction.To.Address]--;
            }
        }

        private async Task<List<BlobInfo>> InvokeTransaction(RecoveryTransaction transaction, AliensRecoveryOptions aliensRecoveryOptions,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {transaction}", transaction);
            var rsyncResult = await _remoteFileCopier.CopyWithRsync(transaction.From, transaction.To, cancellationToken);

            var blobsToRemove = new List<BlobInfo>();
            if (!rsyncResult.IsError)
            {
                _logger.LogDebug("Synced {transaction}", transaction);
                var partitions = _partitionInfoAggregator.GetPartitionInfos(rsyncResult.SyncedFiles);
                foreach (var partition in partitions)
                    blobsToRemove.AddRange(partition.Blobs.Where(b => b.IsClosed));
            }
            else
            {
                aliensRecoveryOptions.LogErrorWithPossibleException<OperationException>(_logger, "Recovery transaction {transaction} failed", transaction);
            }
            return blobsToRemove;
        }

        private async Task RemoveAlreadyMovedFiles(IEnumerable<RecoveryTransaction> transactions,
            CancellationToken cancellationToken = default)
        {
            var cleanedUpDirectories = new HashSet<RemoteDir>();

            foreach (var transaction in transactions)
            {
                var srcFiles = await _filesFinder.FindFiles(transaction.From, cancellationToken);
                var dstFiles = await _filesFinder.FindFiles(transaction.To, cancellationToken);

                var equal = srcFiles
                    .Select(f => (transaction.From, file: f))
                    .ToHashSet(FileInfoComparer.Instance);
                equal.IntersectWith(dstFiles.Select(f => (transaction.To, f)));

                var filesToRemove = equal.Select(t => t.file);

                if (filesToRemove.Any())
                {
                    if (await _remoteFileCopier.RemoveFiles(filesToRemove, cancellationToken))
                        _logger.LogInformation("Successfully removed source files from {dir}", transaction.From);
                    else
                        _logger.LogWarning("Failed to remove source files from {dir}", transaction.From);
                }

                if (!cleanedUpDirectories.Contains(transaction.From))
                {
                    if (await _remoteFileCopier.RemoveEmptySubdirs(transaction.From, cancellationToken))
                    {
                        cleanedUpDirectories.Add(transaction.From);
                        _logger.LogInformation("Successfully removed empty directories at {dir}", transaction.From);
                    }
                    else
                        _logger.LogWarning("Failed to clean up empty directories at {dir}", transaction.From);
                }
            }
        }

        private class FileInfoComparer : IEqualityComparer<(RemoteDir dir, RemoteFileInfo file)>
        {
            public bool Equals((RemoteDir dir, RemoteFileInfo file) x, (RemoteDir dir, RemoteFileInfo file) y)
            {
                return x.file.Checksum == y.file.Checksum
                    && x.file.LengthBytes == y.file.LengthBytes
                    && x.file.Filename.AsSpan(x.dir.Path.Length).SequenceEqual(y.file.Filename.AsSpan(y.dir.Path.Length));
            }

            public int GetHashCode([DisallowNull] (RemoteDir dir, RemoteFileInfo file) obj)
            {
                return HashCode.Combine(obj.file.Checksum);
            }

            public static FileInfoComparer Instance { get; } = new();
        }
    }
}