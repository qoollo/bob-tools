using System;

namespace BobToolsCli.ConfigurationReading
{
    public readonly struct ConfigurationReadingResult<T>
    {
        private readonly T _data;
        private readonly string _error;

        private ConfigurationReadingResult(T data, string error)
        {
            _data = data;
            _error = error;
        }

        public bool IsOk(out T data, out string error)
        {
            data = _data;
            error = _error;
            return _error == null;
        }

        public ConfigurationReadingResult<Y> Map<Y>(Func<T, Y> f)
        {
            if (_error != null)
                return ConfigurationReadingResult<Y>.Error(_error);
            return ConfigurationReadingResult<Y>.Ok(f(_data));
        }

        internal static ConfigurationReadingResult<T> Ok(T data) => new(data, null);
        internal static ConfigurationReadingResult<T> Error(string error) => new(default, error);
    }
}