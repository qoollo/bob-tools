using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    class PhysicalDisk
    {
        public PhysicalDisk(PhysicalId physicalId, DevPath devPath, IList<Volume> volumes)
        {
            PhysicalId = physicalId;
            DevPath = devPath;
            Volumes = volumes ?? throw new ArgumentNullException(nameof(volumes));
        }

        public PhysicalId PhysicalId { get; }
        public DevPath DevPath { get; }
        public IList<Volume> Volumes { get; }

        public override string ToString()
        {
            return $"[{PhysicalId}] {DevPath}, {Volumes.Count} volume{(Volumes.Count == 1 ? "" : "s")}";
        }

        public bool NoVolumes => Volumes.Count == 0;
    }
}
