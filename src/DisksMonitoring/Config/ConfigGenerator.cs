using BobApi;
using BobApi.Entities;
using DisksMonitoring.Entities;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksProcessing;
using DisksMonitoring.OS.DisksProcessing.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.Config
{
    class ConfigGenerator
    {
        private readonly DisksFinder disksFinder;
        private readonly ILogger<ConfigGenerator> logger;

        public ConfigGenerator(DisksFinder disksFinder, ILogger<ConfigGenerator> logger)
        {
            this.disksFinder = disksFinder;
            this.logger = logger;
        }

        public async Task<IEnumerable<BobDisk>> GenerateConfigFromBob(BobApiClient bobApiClient)
        {
            var disks = await bobApiClient.GetDisksToMonitor();
            if (disks == null)
            {
                logger.LogError($"Failed to get bob disks from {bobApiClient}");
                return Enumerable.Empty<BobDisk>();
            }
            var physicalDisks = await disksFinder.FindDisks();
            var infos = disks.Where(d => d.IsActive).Select(d => FindInfo(d, physicalDisks)).Where(i => i != null);
            return infos;
        }

        private static BobDisk FindInfo(Disk bobDisk, IList<PhysicalDisk> physicalDisks)
        {
            var path = new BobPath(bobDisk.Path);
            var disk = physicalDisks.Where(d => d.Volumes.Any(v => v.ContainsPath(path)))
                .OrderByDescending(d => d.Volumes.Where(v => v.ContainsPath(path)).Max(v => v.MountPath.Value.Length))
                .FirstOrDefault();
            if (disk != null)
            {
                var volume = disk.Volumes.Where(v => v.ContainsPath(path)).OrderByDescending(v => v.MountPath.Value.Length).First();
                return new BobDisk(volume.PhysicalId, volume.MountPath.Value, path, bobDisk.Name);
            }
            return null;
        }
    }
}
