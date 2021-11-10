using System;

namespace BobToolsCli
{
    public readonly struct YamlReadingResult<T>
    {
        private readonly T _data;
        private readonly string _error;

        private YamlReadingResult(T data, string error)
        {
            _data = data;
            _error = error;
        }

        public bool IsOk(out T data, out string error)
        {
            data = _data;
            error = _error;
            return _error != null;
        }

        internal static YamlReadingResult<T> Ok(T data) => new(data, null);
        internal static YamlReadingResult<T> Error(string error) => new(default, error);
    }
}