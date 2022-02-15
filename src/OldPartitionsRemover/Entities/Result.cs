using System;
using System.Threading.Tasks;
using BobApi.Entities;
using BobToolsCli;
using BobToolsCli.ConfigurationReading;

namespace OldPartitionsRemover.Entities
{
    public class Result<T>
    {
        private readonly T _data;
        private readonly string _error;

        private Result(T data, string error)
        {
            _data = data;
            _error = error;
        }

        public bool IsOk(out T data, out string error)
        {
            data = _data;
            error = _error;
            return !IsError;
        }

        public async Task<Result<Y>> Bind<Y>(Func<T, Task<Result<Y>>> f)
        {
            if (IsError)
                return Result<Y>.Error(_error);
            return await f(_data);
        }

        public Result<Y> Bind<Y>(Func<T, Result<Y>> f)
        {
            if (IsError)
                return Result<Y>.Error(_error);
            return f(_data);
        }

        public Result<Y> Map<Y>(Func<T, Y> f)
        {
            if (IsError)
                return Result<Y>.Error(_error);
            return Result<Y>.Ok(f(_data));
        }

        public async Task<Result<Y>> Map<Y>(Func<T, Task<Y>> f)
        {
            if (IsError)
                return Result<Y>.Error(_error);
            return Result<Y>.Ok(await f(_data));
        }

        private bool IsError => _error != null;

        public static Result<T> Ok(T data) => new(data, null);
        public static Result<T> Error(string error) => new(default, error);

        public static implicit operator Result<T>(ConfigurationReadingResult<T> r)
        {
            if (r.IsOk(out var d, out var e))
                return Ok(d);
            return Error(e);
        }

        public static implicit operator Result<T>(BobApiResult<T> r)
        {
            if (r.IsOk(out var d, out var e))
                return Ok(d);
            return Error(e.ToString());
        }

        public static async Task<Result<T>> Sequence(Result<Task<T>> r)
        {
            if (r.IsError)
                return Error(r._error);
            return Ok(await r._data);
        }
    }
}