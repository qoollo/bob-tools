using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobAliensRecovery.AliensRecovery.Entities;
using BobToolsCli.Exceptions;
using BobToolsCli.Helpers;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;
using RemoteFileCopy.FilesFinding;

namespace BobAliensRecovery.AliensRecovery
{
    public class BlobsMover
    {
        private readonly IRemoteFileCopier _remoteFileCopier;
        private readonly PartitionInfoAggregator _partitionInfoAggregator;
        private readonly FilesFinder _filesFinder;
        private readonly ParallelP2PProcessor _parallelP2PProcessor;
        private readonly ILogger<BlobsMover> _logger;

        public BlobsMover(IRemoteFileCopier remoteFileCopier,
			  PartitionInfoAggregator partitionInfoAggregator,
			  FilesFinder filesFinder,
			  ParallelP2PProcessor parallelP2PProcessor,
			  ILogger<BlobsMover> logger)
        {
            _remoteFileCopier = remoteFileCopier;
            _partitionInfoAggregator = partitionInfoAggregator;
            _filesFinder = filesFinder;
            _parallelP2PProcessor = parallelP2PProcessor;
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
            var operations = recoveryTransactions.Select(t => ParallelP2PProcessor.CreateOperation(
                t.From.Address, t.To.Address, () => InvokeTransaction(t, aliensRecoveryOptions, cancellationToken)
            ));
            var results = await _parallelP2PProcessor.Invoke(aliensRecoveryOptions.CopyParallelDegree, operations, cancellationToken);
            return results.Where(r => r.Data != null).SelectMany(r => r.Data).ToList();
        }

        private async Task<List<BlobInfo>> InvokeTransaction(RecoveryTransaction transaction, AliensRecoveryOptions aliensRecoveryOptions,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {transaction}", transaction);
            var copyResult = await _remoteFileCopier.Copy(transaction.From, transaction.To, cancellationToken);

            var blobsToRemove = new List<BlobInfo>();
            if (!copyResult.IsError)
            {
                _logger.LogDebug("Synced {transaction}", transaction);
                var partitions = _partitionInfoAggregator.GetPartitionInfos(copyResult.Files);
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
                await _remoteFileCopier.RemoveAlreadyMovedFiles(transaction.From, transaction.To, cancellationToken);

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
    }
}
