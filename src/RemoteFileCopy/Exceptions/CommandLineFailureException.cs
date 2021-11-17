using System;
using System.Runtime.Serialization;

namespace RemoteFileCopy.Exceptions
{
    public class CommandLineFailureException : Exception
    {
        public CommandLineFailureException()
        {
        }

        public CommandLineFailureException(string? message) : base(message)
        {
        }

        public CommandLineFailureException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected CommandLineFailureException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}