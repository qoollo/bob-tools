﻿using System;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    public struct DevPath : IEquatable<DevPath>
    {
        private readonly string data;

        public DevPath(string data)
        {
            if (data is null || data.Length == 0 || !data.StartsWith('/'))
                throw new ArgumentException($"No dev path provided");
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

        public static bool operator ==(DevPath left, DevPath right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DevPath left, DevPath right)
        {
            return !(left == right);
        }
    }
}
