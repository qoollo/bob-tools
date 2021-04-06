using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    struct MountOptions : IEquatable<MountOptions>
    {
        private readonly string data;
        private readonly string[] ops;

        public MountOptions(string data)
        {
            this.data = data;
            ops = data.ToLowerInvariant().Split(',');
        }

        public int Length => data.Length;

        public override bool Equals(object obj)
        {
            return obj is MountOptions path && Equals(path);
        }

        public bool Equals(MountOptions other)
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

        public bool IsRO => ops.Contains("ro");
    }
}
