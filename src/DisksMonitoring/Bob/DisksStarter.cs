using BobApi;
using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS;
using DisksMonitoring.OS.DisksFinding;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace DisksMonitoring.Bob
{
    class DisksStarter
    {
        private readonly DisksMonitor disksMonitor;
        private readonly Configuration configuration;
        private readonly DisksCopier disksCopier;
        private readonly ILogger<DisksStarter> logger;
        private readonly DisksFinder disksFinder;

        public DisksStarter(DisksMonitor disksMonitor, Configuration configuration, DisksCopier disksCopier,
            ILogger<DisksStarter> logger, DisksFinder disksFinder)
        {
            this.disksMonitor = disksMonitor;
            this.configuration = configuration;
            this.disksCopier = disksCopier;
            this.logger = logger;
            this.disksFinder = disksFinder;
        }

        public async Task StartDisks(BobApiClient bobApiClient)
        {
            var disksToStart = configuration.MonitoringEntries;
            var disks = await disksFinder.FindDisks();
            foreach (var i in disksToStart)
            {
                var inactiveDisks = await bobApiClient.GetInactiveDisks();
                if (inactiveDisks == null)
                    return;
                if (!inactiveDisks.Any(d => d.Name == i.DiskNameInBob))
                    continue;

                var disk = disks.FirstOrDefault(d => d.Volumes.Any(v => v.MountPath.Equals(i.MountPath) && v.IsMounted));
                var volume = disk?.Volumes.First(v => v.MountPath.Equals(i.MountPath) && v.IsMounted);
                if (disks.Count == 0 
                    || !disks.Any(d => !d.NoVolumes && d.Volumes.Any(v => v.MountPath.Equals(i.MountPath) && v.IsMounted && !v.IsReadOnly)))
                    continue;

                logger.LogInformation($"Trying to start disk {i}");
                if (!configuration.KnownUuids.Contains(volume.UUID))
                    await disksCopier.CopyDataFromReplica(bobApiClient, i);
                configuration.SaveUUID(await disksMonitor.GetUUID(i));
                logger.LogInformation($"Starting bobdisk {i}...");
                int retry = 0;
                while (!await bobApiClient.StartDisk(i.DiskNameInBob) && retry++ < configuration.StartRetryCount)
                    logger.LogWarning($"Failed to start bobdisk in try {retry}, trying again");
                if (retry == configuration.StartRetryCount)
                    logger.LogError($"Failed to start bobdisk {i}");
                else
                    logger.LogInformation($"Bobdisk {i} started");
            }
        }
    }
}
