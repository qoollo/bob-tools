using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    class LogicalVolume
    {
        public LogicalVolume(PhysicalId physicalId, DevPath devPath, UUID uUID)
        {
            PhysicalId = physicalId;
            DevPath = devPath;
            UUID = uUID;
        }

        public PhysicalId PhysicalId { get; }
        public DevPath DevPath { get; }
        public UUID UUID { get; }

        public override string ToString()
        {
            return $"[{PhysicalId}] {DevPath}";
        }
    }
}
