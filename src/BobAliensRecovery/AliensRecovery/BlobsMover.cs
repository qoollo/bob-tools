using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
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
                await RemoveAlreadyMovedFiles(recoveryTransactions, aliensRecoveryOptions.HashParallelDegree, cancellationToken);
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
                                                   int hashParallelDegree,
                                                   CancellationToken cancellationToken = default)
        {
            if (hashParallelDegree > 1)
            {
                var coordinator = new Coordinator(hashParallelDegree, _remoteFileCopier, _logger);
                await coordinator.RunTasks(transactions, cancellationToken);
            }
            else
                foreach (var transaction in transactions)
                    await _remoteFileCopier.RemoveAlreadyMovedFiles(transaction.From, transaction.To, cancellationToken);

            var dirsToCleanUp = transactions.Select(t => t.From).Distinct();
            foreach (var dir in dirsToCleanUp)
            {
                if (await _remoteFileCopier.RemoveEmptySubdirs(dir, cancellationToken))
                    _logger.LogInformation("Successfully removed empty directories at {dir}", dir);
                else
                    _logger.LogWarning("Failed to clean up empty directories at {dir}", dir);
            }
        }

        private class Coordinator
        {
            private readonly int _degree;
            private readonly Channel<RecoveryTransaction?> _requests = Channel.CreateUnbounded<RecoveryTransaction?>();
            private readonly Channel<RecoveryTransaction> _responses = Channel.CreateUnbounded<RecoveryTransaction>();
            private IRemoteFileCopier _remoteFileCopier;
            private ILogger _logger;

            public Coordinator(int degree, IRemoteFileCopier remoteFileCopier, ILogger logger)
            {
                _degree = degree;
                _remoteFileCopier = remoteFileCopier;
                _logger = logger;
            }

            public async Task RunTasks(IEnumerable<RecoveryTransaction> transactions,
                                       CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workersTask = Task.WhenAll(Enumerable.Range(0, _degree).Select(_ => Worker(cancellationToken)));
                var transactionsByPair = transactions
                    .GroupBy(t => (t.From.Address, t.To.Address))
                    .ToDictionary(g => g.Key, g => g.ToList());
                var banned = new HashSet<IPAddress>();
                int total = transactions.Count(), count = 0, step = total / 10;
                while (transactionsByPair.Count > 0)
                {
                    (IPAddress, IPAddress) nextTransactionKey;
                    do
                    {
                        nextTransactionKey = transactionsByPair.Keys.FirstOrDefault(k => !banned.Contains(k.Item1) && !banned.Contains(k.Item2));
                        if (nextTransactionKey != default)
                        {
                            var availableTransactions = transactionsByPair[nextTransactionKey];
                            var transaction = availableTransactions[0];
                            availableTransactions.RemoveAt(0);
                            if (availableTransactions.Count == 0)
                                transactionsByPair.Remove(nextTransactionKey);
                            banned.Add(nextTransactionKey.Item1);
                            banned.Add(nextTransactionKey.Item2);
                            await _requests.Writer.WriteAsync(transaction);
                        }
                    }
                    while (nextTransactionKey != default);
                    var completed = await _responses.Reader.ReadAsync();
                    banned.Remove(completed.From.Address);
                    banned.Remove(completed.To.Address);
                    count++;
                    if (count % step == 0)
                        _logger.LogInformation("Completed {}/{} hash transactions", count, total);
                }
                for (var i = 0; i < _degree; i++)
                    await _requests.Writer.WriteAsync(null);
                await workersTask;
            }

            private async Task Worker(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var transaction = await _requests.Reader.ReadAsync();
                while(transaction != null)
                {
                    await _remoteFileCopier.RemoveAlreadyMovedFiles(transaction.From, transaction.To, cancellationToken);

                    await _responses.Writer.WriteAsync(transaction);
                    transaction = await _requests.Reader.ReadAsync();
                }
            }
        }
    }
}
