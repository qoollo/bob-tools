using System;

namespace BobApi.Entities
{
    public readonly struct BobApiResult<T>
    {
        private readonly T _data;
        private readonly ErrorType? _errorType;

        private BobApiResult(T data, ErrorType? errorType)
        {
            _data = data;
            _errorType = errorType;
        }

        public bool TryGetData(out T data)
        {
            data = _data;
            return _errorType == null;
        }

        public bool TryGetError(out ErrorType errorType)
        {
            errorType = _errorType ?? 0;
            return _errorType != null;
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

        internal static BobApiResult<T> Ok(T data) => new BobApiResult<T>(data, null);
        internal static BobApiResult<T> Unavailable() => new BobApiResult<T>(default, ErrorType.NodeIsUnavailable);
        internal static BobApiResult<T> Unsuccessful() => new BobApiResult<T>(default, ErrorType.UnsuccessfulResponse);
    }
}