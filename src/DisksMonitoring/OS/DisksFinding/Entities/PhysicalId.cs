using System;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    public class PhysicalId : IEquatable<PhysicalId>
    {
        private const string delim = "|";
        private readonly string data;
        private readonly PhysicalId upperPhysicalId;

        public PhysicalId(string data, PhysicalId upperPhysicalId)
        {
            this.data = data;
            this.upperPhysicalId = upperPhysicalId;
        }

        public override bool Equals(object obj)
        {
            return obj is PhysicalId id && Equals(id);
        }

        public bool Equals(PhysicalId other)
        {
            if (other is null)
                return false;
            else if (upperPhysicalId != null && !upperPhysicalId.Equals(other.upperPhysicalId))
                return false;
            else if ((upperPhysicalId == null) != (other.upperPhysicalId == null))
                return false;

            return data == other.data;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(data);
        }

        public override string ToString()
        {
            string res = string.Empty;
            if (upperPhysicalId != null)
                res = upperPhysicalId.ToString() + delim;
            return res + data;
        }

        public static PhysicalId FromString(string repr)
        {
            var split = repr.Split(delim);
            PhysicalId res = null;
            foreach (var id in split)
                res = new PhysicalId(id, res);
            return res;
        }

        public bool IsChildOfOrEqualTo(PhysicalId parent)
        {
            return Equals(parent) || upperPhysicalId?.Equals(parent) == true;
        }
    }
}
