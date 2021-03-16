using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    class UUID : IEquatable<UUID>
    {
        private readonly string data;

        public UUID(string data)
        {
            this.data = data;
        }

        public override bool Equals(object obj)
        {
            return obj is UUID uUID && Equals(uUID);
        }

        public bool Equals(UUID other)
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

        public static explicit operator string(UUID uUID) => uUID.data;
    }
}
