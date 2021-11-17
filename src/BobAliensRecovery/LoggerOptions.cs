using System;
using CommandLine;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery
{
    class LoggerOptions
    {
        public LoggerOptions(int verbosityLevel)
        {
            MinLevel = verbosityLevel switch
            {
                3 => LogLevel.Trace,
                2 => LogLevel.Information,
                1 => LogLevel.Error,
                0 => LogLevel.None,
                _ => throw new ArgumentException("Verbosity must be in range [0; 3]")
            };
        }

        public LogLevel MinLevel { get; }
    }
}