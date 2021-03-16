using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.DisksProcessing.Entities
{
    class BobPath : IEquatable<BobPath>
    {
        private readonly string data;

        public BobPath(string data)
        {
            this.data = data;
        }

        public bool StartsWith(MountPath path) => data.StartsWith(path.Path);

        public override bool Equals(object obj)
        {
            return Equals(obj as BobPath);
        }

        public bool Equals(BobPath other)
        {
            return other != null &&
                   data == other.data;
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
