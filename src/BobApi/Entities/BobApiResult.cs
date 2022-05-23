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

        public BobApiResult<Y> Map<Y>(Func<T, Y> f)
        {
            if (IsError)
                return new BobApiResult<Y>(default, _errorType);
            return new BobApiResult<Y>(f(_data), _errorType);
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

        public static BobApiResult<T> Ok(T data) => new BobApiResult<T>(data, null);

        public static BobApiResult<T> Unavailable() => new BobApiResult<T>(default, BobApiError.NodeIsUnavailable());
        public static BobApiResult<T> Unsuccessful(HttpMethod requestMethod, Uri requestUri, string content)
            => new BobApiResult<T>(default, BobApiError.UnsuccessfulResponse(requestMethod, requestUri, content));
    }
}