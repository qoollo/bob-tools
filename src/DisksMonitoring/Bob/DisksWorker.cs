using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BobApi;
using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using Microsoft.Extensions.Logging;

namespace DisksMonitoring.Bob
{
    class DisksWorker
    {
        private readonly DisksMonitor disksMonitor;
        private readonly Configuration configuration;
        private readonly DisksCopier disksCopier;
        private readonly ILogger<DisksWorker> logger;
        private readonly DisksFinder disksFinder;

        public DisksWorker(
            DisksMonitor disksMonitor,
            Configuration configuration,
            DisksCopier disksCopier,
            ILogger<DisksWorker> logger,
            DisksFinder disksFinder
        )
        {
            this.disksMonitor = disksMonitor;
            this.configuration = configuration;
            this.disksCopier = disksCopier;
            this.logger = logger;
            this.disksFinder = disksFinder;
        }

        public async Task AlterBobDisks(BobApiClient bobApiClient)
        {
            await StopDisks(bobApiClient);
            await StartDisks(bobApiClient);
        }

        private async Task StartDisks(BobApiClient bobApiClient)
        {
            var disks = await disksFinder.FindDisks();
            if (disks.Count == 0)
                return;

            foreach (var disk in configuration.MonitoringEntries)
            {
                var inactiveDisksResult = await bobApiClient.GetInactiveDisks();
                if (!inactiveDisksResult.IsOk(out var inactiveDisks, out var err))
                {
                    logger.LogError("Failed to get inactive disks info from bob: {Error}", err);
                    return;
                }
                if (!DiskIsInactive(inactiveDisks, disk))
                    continue;

                var volume = FindFittingVolume(disks, disk);
                if (volume == null)
                    continue;

                if (!VolumeIsKnown(volume))
                    await CopyDataToNewVolume(bobApiClient, disk, volume);

                await ChangeDiskState("started", async s => await bobApiClient.StartDisk(s), disk);
            }
        }

        private async Task StopDisks(BobApiClient bobApiClient)
        {
            var disks = await disksFinder.FindDisks();
            if (disks.Count == 0)
                return;

            foreach (var disk in configuration.MonitoringEntries)
            {
                var inactiveDisksResult = await bobApiClient.GetInactiveDisks();
                if (!inactiveDisksResult.IsOk(out var inactiveDisks, out var err))
                {
                    logger.LogError("Failed to get inactive disks info from bob: {Error}", err);
                    return;
                }
                if (DiskIsInactive(inactiveDisks, disk))
                    continue;

                var volume = FindFittingVolume(disks, disk);
                if (volume != null)
                    continue;

                await ChangeDiskState("stopped", async s => await bobApiClient.StopDisk(s), disk);
            }
        }

        private async Task ChangeDiskState(
            string state,
            Func<string, Task<BobApi.Entities.BobApiResult<bool>>> changeByName,
            BobDisk disk
        )
        {
            logger.LogInformation("Changing state of {Disk} to {State}...", disk, state);
            int retry = 0;
            while (
                !await TryChange(changeByName(disk.DiskNameInBob))
                && retry++ < configuration.StartRetryCount
            )
                logger.LogWarning(
                    "Failed to change state of {Disk} to {State} in try {Try}, trying again",
                    disk,
                    state,
                    retry
                );
            if (retry == configuration.StartRetryCount)
                logger.LogError("Failed to change state of {Disk} to {State}", disk, state);
            else
                logger.LogInformation(
                    "Successfully changed state of {Disk} to {State}",
                    disk,
                    state
                );
        }

        private static async Task<bool> TryChange(Task<BobApi.Entities.BobApiResult<bool>> task) =>
            (await task).TryGetData(out var isChanged) && isChanged;

        private async Task CopyDataToNewVolume(
            BobApiClient bobApiClient,
            BobDisk diskToStart,
            Volume volume
        )
        {
            logger.LogInformation("Volume {Volume} is unknown, copying data from replicas", volume);
            await disksCopier.CopyDataFromReplica(bobApiClient, diskToStart);
            configuration.SaveUUID(volume.UUID);
        }

        private bool VolumeIsKnown(Volume volume) => configuration.KnownUuids.Contains(volume.UUID);

        private static Volume FindFittingVolume(List<PhysicalDisk> disks, BobDisk diskToStart) =>
            disks
                .SelectMany(d => d.Volumes)
                .FirstOrDefault( v =>
                    v.MountPath.Equals(diskToStart.MountPath)
                    && v.IsMounted
                    && !v.IsReadOnly
                );

        private bool DiskIsInactive(
            IEnumerable<BobApi.Entities.Disk> inactiveDisks,
            Entities.BobDisk disk
        ) => inactiveDisks.Any(d => d.Name == disk.DiskNameInBob);
    }
}
