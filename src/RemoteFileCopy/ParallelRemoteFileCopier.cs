using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Rsync.Entities;

namespace RemoteFileCopy
{
    internal class ParallelRemoteFileCopier
    {
        private readonly List<(RemoteDir from, RemoteDir to)> _remaining;
        private readonly Dictionary<IPAddress, int> _countByAddress;
        private readonly int _maxDegreeOfParallelism;
        private readonly Func<RemoteDir, RemoteDir, CancellationToken, Task<RsyncResult>> _copy;

        public ParallelRemoteFileCopier(int maxDegreeOfParallelism,
            IEnumerable<(RemoteDir from, RemoteDir to)> transactions,
            Func<RemoteDir, RemoteDir, CancellationToken, Task<RsyncResult>> copy)
        {
            _remaining = transactions.ToList();
            _countByAddress = transactions.SelectMany(t => new[] { t.from.Address, t.to.Address }).Distinct()
                .ToDictionary(a => a, _ => 0);
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _copy = copy;
        }

        public async Task<(RemoteDir from, RemoteDir to, RsyncResult res)[]> Copy(CancellationToken cancellationToken = default)
        {
            var results = new (RemoteDir from, RemoteDir to, RsyncResult res)[_remaining.Count];
            await Parallel.ForEachAsync(Enumerable.Range(0, _remaining.Count),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = _maxDegreeOfParallelism
                },
                async (ind, t) =>
                {
                    var transaction = TakeTransaction();
                    try
                    {
                        results[ind] = (transaction.from, transaction.to, await _copy(transaction.from, transaction.to, t));
                    }
                    finally
                    {
                        CleanUp(transaction);
                    }
                });
            return results;
        }

        private (RemoteDir from, RemoteDir to) TakeTransaction()
        {
            (RemoteDir from, RemoteDir to) transaction;
            lock (_countByAddress)
            {
                transaction = _remaining.OrderBy(t => _countByAddress[t.from.Address] + _countByAddress[t.to.Address]).First();
                _remaining.Remove(transaction);
                _countByAddress[transaction.from.Address]++;
                _countByAddress[transaction.to.Address]++;
            }

            return transaction;
        }

        private void CleanUp((RemoteDir from, RemoteDir to) transaction)
        {
            lock (_countByAddress)
            {
                _countByAddress[transaction.from.Address]--;
                _countByAddress[transaction.to.Address]--;
            }
        }

    }
}