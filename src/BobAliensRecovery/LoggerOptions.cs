using System;
using CommandLine;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery
{
    class LoggerOptions
    {
        private readonly int _verbosityLevel;

        public LoggerOptions(int verbosityLevel)
        {
            _verbosityLevel = verbosityLevel;
        }

        public LogLevel MinLevel
        {
            get
            {
                return _verbosityLevel switch
                {
                    3 => LogLevel.Trace,
                    2 => LogLevel.Debug,
                    1 => LogLevel.Information,
                    0 => LogLevel.Error,
                    _ => throw new ArgumentException("Verbosity must be in range [0; 3]")
                };
            }
        }
    }
}