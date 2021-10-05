using System;
using System.Runtime.Serialization;

namespace BobAliensRecovery.Exceptions
{
    public class ClusterStateException : Exception
    {
        public ClusterStateException()
        {
        }

        public ClusterStateException(string? message) : base(message)
        {
        }

        public ClusterStateException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected ClusterStateException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}