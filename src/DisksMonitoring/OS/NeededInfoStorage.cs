using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksProcessing.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DisksMonitoring.OS
{
    class NeededInfoStorage
    {
        private readonly Configuration configuration;
        private readonly ILogger<NeededInfoStorage> logger;

        public NeededInfoStorage(Configuration configuration, ILogger<NeededInfoStorage> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public MountPath? FindMountPath(Volume volume)
        {
            var bobDisk = FindBobDisk(volume);
            if (bobDisk != null)
                return bobDisk.MountPath;
            logger.LogDebug($"Mount path for {volume} not found");
            return null;
        }

        public BobPath FindBobPath(Volume volume)
        {
            var bobDisk = FindBobDisk(volume);
            return bobDisk?.BobPath;
        }

        public BobDisk FindBobDisk(Volume volume) => configuration.MonitoringEntries.FirstOrDefault(i => i.PhysicalId.Equals(volume.PhysicalId));

        public bool ShouldBeMounted(Volume volume) => FindMountPath(volume) != null;

        public bool ShouldBeProcessed(PhysicalDisk physicalDisk) =>
            configuration.MonitoringEntries.Any(i => i.PhysicalId.IsChildOfOrEqualTo(physicalDisk.PhysicalId));

        public bool ShouldBeProcessed(Volume volume) => configuration.MonitoringEntries.Any(i => i.PhysicalId.Equals(volume.PhysicalId));

        public bool IsProtected(Volume volume) => configuration.KnownUuids.Contains(volume.UUID);
    }
}
