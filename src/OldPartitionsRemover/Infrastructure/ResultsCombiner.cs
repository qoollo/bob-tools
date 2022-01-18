using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BobToolsCli;
using Microsoft.Extensions.Logging;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.Infrastructure
{
    public class ResultsCombiner
    {
        private readonly CommonArguments _commonArguments;
        private readonly ILogger<ResultsCombiner> _logger;

        public ResultsCombiner(CommonArguments commonArguments,
            ILogger<ResultsCombiner> logger)
        {
            _commonArguments = commonArguments;
            _logger = logger;
        }

        public async Task<Result<List<Y>>> CollectResults<T, Y>(IEnumerable<T> elems, Func<T, Task<Result<Y>>> f)
        {
            return await CombineResults(elems, new List<Y>(),
                async (l, p) => (await f(p)).Map(part => { l.Add(part); return l; }));
        }

        public async Task<Result<List<Y>>> CollectResults<T, Y>(IEnumerable<T> elems, Func<T, Task<Result<List<Y>>>> f)
        {
            return await CombineResults(elems, new List<Y>(),
                async (l, p) => (await f(p)).Map(part => { l.AddRange(part); return l; }));
        }

        public async Task<Result<Y>> CombineResults<T, Y>(IEnumerable<T> elems, Y seed, Func<Y, T, Task<Result<Y>>> f)
        {
            return await elems.Aggregate(
                Task.FromResult(Result<Y>.Ok(seed)),
                (task, next) => task.ContinueWith(async resultTask =>
                {
                    var result = await resultTask;
                    var nextResult = await Combine(f, next, result);
                    return SelectBestResult(result, nextResult);
                }).Unwrap());
        }

        private Result<Y> SelectBestResult<Y>(Result<Y> prevResult, Result<Y> nextResult)
        {
            if (!nextResult.IsOk(out var _, out var err) && _commonArguments.ContinueOnError)
            {
                _logger.LogError("Error: {Error}", err);
                return prevResult;
            }
            return nextResult;
        }

        private static async Task<Result<Y>> Combine<T, Y>(Func<Y, T, Task<Result<Y>>> f, T next, Result<Y> result)
        {
            var combined = await result.Map(v => f(v, next));
            return combined.Bind(_ => _);
        }
    }
}