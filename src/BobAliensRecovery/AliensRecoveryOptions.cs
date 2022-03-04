using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery
{
    class AliensRecoveryOptions
    {
        private static readonly Regex s_curlyRegex = new(@"\{.*?\D.*?\}");

        public AliensRecoveryOptions(bool removeCopied,
            bool continueOnError,
            bool restartNodes,
            int copyParallelDegree)
        {
            RemoveCopied = removeCopied;
            ContinueOnError = continueOnError;
            RestartNodes = restartNodes;
            CopyParallelDegree = copyParallelDegree;
        }

        public bool RemoveCopied { get; }

        public bool ContinueOnError { get; }

        public bool RestartNodes { get; }
        public int CopyParallelDegree { get; }

        public void LogErrorWithPossibleException<E>(ILogger logger, string format, params object[] args)
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
                    format = ChangeNamesToPos(format);

                    if (cons.Invoke(new[] { string.Format(format, args) }) is E e)
                        throw e;

                    throw new ArgumentException($"Failed to initialize create type {typeof(E).Name}");
                }
                throw new ArgumentException($"Failed to construct type {typeof(E).Name}");
            }
        }

        private static string ChangeNamesToPos(string format)
        {
            var pos = 0;
            while (s_curlyRegex.IsMatch(format))
                format = s_curlyRegex.Replace(format, $"{{{pos++}}}", 1);
            return format;
        }
    }
}