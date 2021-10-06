using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery
{
    class AliensRecoveryOptions
    {
        public AliensRecoveryOptions(bool removeCopied,
            bool continueOnError)
        {
            RemoveCopied = removeCopied;
            ContinueOnError = continueOnError;
        }

        public bool RemoveCopied { get; }

        public bool ContinueOnError { get; }

        public void LogError<E>(ILogger logger, string format, params object[] args)
            where E : Exception, new()
        {
            logger.LogError(format, args);
            if (!ContinueOnError)
            {
                var cons = typeof(E).GetConstructors().FirstOrDefault(c =>
                    c.GetParameters().Length == 1
                    && c.GetParameters()[0].ParameterType == typeof(string)
                    && c.IsPublic);
                if (cons != null)
                {
                    if (cons.Invoke(new[] { string.Format(format, args) }) is E e)
                        throw e;

                    throw new ArgumentException($"Failed to initialize create type {typeof(E).Name}");
                }
                throw new ArgumentException($"Failed to construct type {typeof(E).Name}");
            }
        }
    }
}