using System;
using System.Net;

namespace RemoteFileCopy.Entities
{
    public class RemoteDir : IEquatable<RemoteDir>
    {
        public RemoteDir(IPAddress address, string path)
        {
            Address = address;
            Path = path;
        }

        public IPAddress Address { get; }
        public string Path { get; }

        public override string ToString()
        {
            return $"{Address}:{Path}";
        }

        public bool Equals(RemoteDir? other)
        {
            if (other is null)
                return false;
            return (Address, Path).Equals((other.Address, other.Path));
        }

        public override bool Equals(object? obj)
        {
            return obj is RemoteDir rd && Equals(rd);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Address, Path);
        }
    }
}