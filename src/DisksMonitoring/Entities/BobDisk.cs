using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksProcessing.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.Entities
{
    class BobDisk : IEquatable<BobDisk>
    {
        public BobDisk() { }
        public BobDisk(PhysicalId physicalId, MountPath mountPath, BobPath bobPath, string diskNameInBob)
        {
            PhysicalId = physicalId ?? throw new ArgumentNullException(nameof(physicalId));
            MountPath = mountPath;
            BobPath = bobPath ?? throw new ArgumentNullException(nameof(bobPath));
            DiskNameInBob = diskNameInBob;
        }

        public PhysicalId PhysicalId { get; set; }
        public MountPath MountPath { get; set; }
        public BobPath BobPath { get; set; }
        public string DiskNameInBob { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as BobDisk);
        }

        public bool Equals(BobDisk other)
        {
            return other != null &&
                   EqualityComparer<PhysicalId>.Default.Equals(PhysicalId, other.PhysicalId) &&
                   MountPath.Equals(other.MountPath) &&
                   EqualityComparer<BobPath>.Default.Equals(BobPath, other.BobPath);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PhysicalId, MountPath, BobPath);
        }

        public override string ToString()
        {
            return $"{PhysicalId}, {MountPath}, {BobPath}, {DiskNameInBob}";
        }
    }
}
