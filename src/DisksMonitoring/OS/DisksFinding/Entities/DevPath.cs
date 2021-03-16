using System;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    public struct DevPath : IEquatable<DevPath>
    {
        private readonly string data;

        public DevPath(string data)
        {
            this.data = data;
        }

        public override bool Equals(object obj)
        {
            return obj is DevPath path && Equals(path);
        }

        public bool Equals(DevPath other)
        {
            return data == other.data;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(data);
        }

        public override string ToString()
        {
            return data;
        }

        public string Path => data;
    }
}
