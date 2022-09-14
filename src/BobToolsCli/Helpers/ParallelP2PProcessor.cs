using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BobToolsCli.Helpers
{
    public class ParallelP2PProcessor
    {
        private readonly int? _perNodeLimit;
        private readonly SemaphoreSlim _takeWaker;

        public ParallelP2PProcessor(int? perNodeLimit = null)
        {
            _perNodeLimit = perNodeLimit;
            _takeWaker = new SemaphoreSlim(1);
        }

        public async Task<Result<T>[]> Invoke<T>(int maxDegreeOfParallelism,
					   IEnumerable<Operation<T>> operations,
					   CancellationToken cancellationToken = default)
        {
            var remaining = operations?.ToList() ?? throw new ArgumentNullException(nameof(operations));
            var countByAddress = operations.SelectMany(o => new[] { o.From, o.To }).Distinct().ToDictionary(a => a, _ => 0);
            var results = new Result<T>[remaining.Count];
            await Parallel.ForEachAsync(Enumerable.Range(0, remaining.Count),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism
                },
                async (ind, t) =>
                {
                    var operation = await TakeOperation(countByAddress, remaining, t);
                    try
                    {
                        results[ind] = await operation.Invoke();
                    }
                    finally
                    {
                        CleanUp(countByAddress, operation);
                    }
                });
            return results;
        }

        public static Operation<T> CreateOperation<T>(IPAddress from, IPAddress to, Func<Task<T>> func) => new(from, to, func);

        private async ValueTask<Operation<T>> TakeOperation<T>(Dictionary<IPAddress, int> countByAddress, List<Operation<T>> remaining, CancellationToken cancellationToken)
        {
            while (true)
            {
                lock (countByAddress)
                {
                    var operation = remaining.OrderBy(t => countByAddress[t.From] + countByAddress[t.To]).First();

                    if (_perNodeLimit == null
                        || (countByAddress[operation.From] < _perNodeLimit
                            && countByAddress[operation.To] < _perNodeLimit))
                    {
                        remaining.Remove(operation);
                        countByAddress[operation.From]++;
                        countByAddress[operation.To]++;
                        return operation;
                    }
                }
                await _takeWaker.WaitAsync(cancellationToken);
            }
        }

        private void CleanUp<T>(Dictionary<IPAddress, int> countByAddress, Operation<T> operation)
        {
            lock (countByAddress)
            {
                countByAddress[operation.From]--;
                countByAddress[operation.To]--;
            }
            _takeWaker.Release(2);
        }

        public struct Operation<T>
        {
            public Operation(IPAddress from, IPAddress to, Func<Task<T>> func)
            {
                From = from;
                To = to;
                Func = func;
            }

            public IPAddress From { get; }
            public IPAddress To { get; }
            public Func<Task<T>> Func { get; }

            internal async Task<Result<T>> Invoke() => new(From, To, await Func());
        }

        public struct Result<T>
        {
            public Result(IPAddress from, IPAddress to, T data)
            {
                From = from;
                To = to;
                Data = data;
            }

            public IPAddress From { get; }
            public IPAddress To { get; }
            public T Data { get; }
        }
    }
}
