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
                    var operation = TakeOperation(countByAddress, remaining);
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

        private Operation<T> TakeOperation<T>(Dictionary<IPAddress, int> countByAddress, List<Operation<T>> remaining)
        {
            Operation<T> operation;
            lock (countByAddress)
            {
                operation = remaining.OrderBy(t => countByAddress[t.From] + countByAddress[t.To]).First();
                remaining.Remove(operation);
                countByAddress[operation.From]++;
                countByAddress[operation.To]++;
            }

            return operation;
        }

        private void CleanUp<T>(Dictionary<IPAddress, int> countByAddress, Operation<T> operation)
        {
            lock (countByAddress)
            {
                countByAddress[operation.From]--;
                countByAddress[operation.To]--;
            }
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
