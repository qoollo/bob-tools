using System;
using CommandLine;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery
{
    class LoggerOptions
    {
        public LoggerOptions(LogLevel minLevel)
        {
            MinLevel = minLevel;
        }

        public LogLevel MinLevel { get; }
    }
}