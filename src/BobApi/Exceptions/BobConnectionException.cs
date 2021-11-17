using System;
using System.Runtime.Serialization;

namespace BobApi.Exceptions
{
    public class BobConnectionException : Exception
    {
        public BobConnectionException(Uri nodeUri, Exception innerException) : base("", innerException)
        {
            NodeUri = nodeUri;
        }

        public Uri NodeUri { get; }
    }
}