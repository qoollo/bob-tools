using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    struct MountPath : IEquatable<MountPath>
    {
        private readonly string data;

        public MountPath(string data)
        {
            this.data = data;
        }

        public int Length => data.Length;

        public override bool Equals(object obj)
        {
            return obj is MountPath path && Equals(path);
        }

        public bool Equals(MountPath other)
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
