using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BobApi.Entities
{
    public readonly struct BobApiResult<T>
    {
        private readonly T _data;
        private readonly BobApiError _errorType;

        private BobApiResult(T data, BobApiError errorType)
        {
            _data = data;
            _errorType = errorType;
        }

        public bool IsError => _errorType != null;

        public bool IsOk(out T data, out BobApiError errorType)
        {
            data = _data;
            errorType = _errorType;
            return !IsError;
        }

        public bool TryGetData(out T data)
        {
            data = _data;
            return !IsError;
        }

        public bool TryGetError(out BobApiError errorType)
        {
            errorType = _errorType;
            return IsError;
        }

        public override string ToString()
        {
            if (TryGetData(out var d))
                return $"Ok({d})";
            else if (TryGetError(out var e))
                return $"Error({e})";
            else
                return "Unknown result state";
        }

        public BobApiResult<Y> Map<Y>(Func<T, Y> f)
        {
            return new BobApiResult<Y>(IsError ? default : f(_data), _errorType);
        }

        public BobApiResult<Y> Bind<Y>(Func<T, BobApiResult<Y>> f)
        {
            if (IsError)
                return new BobApiResult<Y>(default, _errorType);
            return f(_data);
        }

        public async Task<BobApiResult<Y>> Bind<Y>(Func<T, Task<BobApiResult<Y>>> f)
        {
            if (IsError)
                return new BobApiResult<Y>(default, _errorType);
            return await f(_data);
        }

        public static BobApiResult<T> Ok(T data) => new BobApiResult<T>(data, null);
        public static async Task<BobApiResult<T>> Traverse(BobApiResult<Task<T>> r)
        {
            if (r.IsError)
                return new BobApiResult<T>(default, r._errorType);
            return Ok(await r._data);
        }

        public static BobApiResult<T[]> Traverse(BobApiResult<T>[] r)
        {
            var result = new T[r.Length];
            for (var i = 0; i < r.Length; i++)
                if (r[i].IsError)
                    return new BobApiResult<T[]>(default, r[i]._errorType);
                else
                    result[i] = r[i]._data;
            return BobApiResult<T[]>.Ok(result);
        }

        internal static BobApiResult<T> Unavailable() => new BobApiResult<T>(default, BobApiError.NodeIsUnavailable());
        internal static BobApiResult<T> Unsuccessful(HttpResponseMessage response) => new BobApiResult<T>(default, BobApiError.UnsuccessfulResponse(response));
    }
}