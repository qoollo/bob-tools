using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Entities;

namespace BobToolsCli.Helpers
{
    public class ParallelP2PProcessor<T>
    {
        private readonly int _maxDegreeOfParallelism;
        private readonly List<Operation> _remaining;
        private readonly Dictionary<IPAddress, int> _countByAddress;

        public ParallelP2PProcessor(int maxDegreeOfParallelism, IEnumerable<Operation> operations)
        {
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _remaining = operations?.ToList() ?? throw new ArgumentNullException(nameof(operations));
            _countByAddress = operations.SelectMany(o => new[] { o.From, o.To }).Distinct().ToDictionary(a => a, _ => 0);
        }

        public async Task<Result[]> Invoke(CancellationToken cancellationToken = default)
        {
            var results = new Result[_remaining.Count];
            await Parallel.ForEachAsync(Enumerable.Range(0, _remaining.Count),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = _maxDegreeOfParallelism
                },
                async (ind, t) =>
                {
                    var operation = TakeOperation();
                    try
                    {
                        results[ind] = await operation.Invoke();
                    }
                    finally
                    {
                        CleanUp(operation);
                    }
                });
            return results;
        }

        private Operation TakeOperation()
        {
            Operation operation;
            lock (_countByAddress)
            {
                operation = _remaining.OrderBy(t => _countByAddress[t.From] + _countByAddress[t.To]).First();
                _remaining.Remove(operation);
                _countByAddress[operation.From]++;
                _countByAddress[operation.To]++;
            }

            return operation;
        }

        private void CleanUp(Operation operation)
        {
            lock (_countByAddress)
            {
                _countByAddress[operation.From]--;
                _countByAddress[operation.To]--;
            }
        }

        public struct Operation
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

            internal async Task<Result> Invoke() => new(From, To, await Func());
        }

        public struct Result
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