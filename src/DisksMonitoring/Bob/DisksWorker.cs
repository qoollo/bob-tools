using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using Microsoft.Extensions.Logging;

#nullable enable
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

        public async Task AlterBobDisks(
            ClusterConfiguration clusterConfiguration,
            string nodeName,
            BobApiClient bobApiClient
        )
        {
            var monitoredVolumes = await FindVolumesWithBob();
            if (monitoredVolumes.Length == 0)
                return;
            var (toStart, toStop) = await GetDisksFromBob(bobApiClient, monitoredVolumes);

            if (configuration.AllowDisksCopy)
                await CopyDisks(clusterConfiguration, nodeName, bobApiClient, toStart);

            await StopDisks(bobApiClient, toStop.Select(v => v.BobDisk));
            await StartDisks(bobApiClient, toStart.Select(v => v.BobDisk));
        }

        private async Task CopyDisks(
            ClusterConfiguration clusterConfiguration,
            string nodeName,
            BobApiClient bobApiClient,
            IEnumerable<VolumeWithBob> volumesWithBob
        )
        {
            foreach (var volumeWithBob in volumesWithBob)
            {
                if (volumeWithBob.Volume == null)
                {
                    logger.LogWarning(
                        "Expected to find volume for disk {Disk}, found none",
                        volumeWithBob.BobDisk
                    );
                    continue;
                }

                if (!volumeWithBob.VolumeIsKnown)
                    await CopyDataToNewVolume(
                        clusterConfiguration,
                        nodeName,
                        bobApiClient,
                        volumeWithBob
                    );
            }
        }

        private async Task StartDisks(BobApiClient bobApiClient, IEnumerable<BobDisk> disks)
        {
            foreach (var disk in disks)
            {
                await ChangeDiskState("started", async s => await bobApiClient.StartDisk(s), disk);
            }
        }

        private async Task StopDisks(BobApiClient bobApiClient, IEnumerable<BobDisk> disks)
        {
            foreach (var disk in disks)
            {
                await ChangeDiskState("stopped", async s => await bobApiClient.StopDisk(s), disk);
            }
        }

        private async Task<(
            List<VolumeWithBob> toStart,
            List<VolumeWithBob> toStop
        )> GetDisksFromBob(BobApiClient bobApiClient, IEnumerable<VolumeWithBob> volumesWithBob)
        {
            List<VolumeWithBob> toStart = new(),
                toStop = new();
            var inactiveDisksResult = await bobApiClient.GetInactiveDisks();
            if (!inactiveDisksResult.IsOk(out var inactiveDisks, out var err))
            {
                logger.LogError("Failed to get inactive disks info from bob: {Error}", err);
            }
            else
            {
                var inactiveDiskNames = inactiveDisks.Select(d => d.Name).ToHashSet();
                foreach (var volumeWithBob in volumesWithBob)
                {
                    var inactiveInBob = inactiveDiskNames.Contains(
                        volumeWithBob.BobDisk.DiskNameInBob
                    );
                    if (inactiveInBob && volumeWithBob.PhysicalExists)
                        toStart.Add(volumeWithBob);
                    else if (!inactiveInBob && !volumeWithBob.PhysicalExists)
                        toStop.Add(volumeWithBob);
                }
            }
            return (toStart, toStop);
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
            {
                logger.LogWarning(
                    "Failed to change state of {Disk} to {State} in try {Try}, trying again",
                    disk,
                    state,
                    retry
                );
            }
            if (retry == configuration.StartRetryCount + 1)
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
            ClusterConfiguration clusterConfiguration,
            string nodeName,
            BobApiClient bobApiClient,
            VolumeWithBob volumeWithBob
        )
        {
            logger.LogInformation(
                "Volume {Volume} is unknown, copying data from replicas",
                volumeWithBob.Volume
            );
            await disksCopier.CopyDataFromReplica(
                clusterConfiguration,
                bobApiClient,
                nodeName,
                volumeWithBob.BobDisk
            );
            configuration.SaveUUID(volumeWithBob.Volume!.UUID);
        }

        private async Task<VolumeWithBob[]> FindVolumesWithBob()
        {
            var physicalDisks = await disksFinder.FindDisks();
            if (physicalDisks.Count == 0)
            {
                logger.LogInformation("No physical disks found");
            }
            var fittingVolumesByMountPath = physicalDisks
                .SelectMany(d => d.Volumes)
                .Where(v => v.IsMounted && !v.IsReadOnly)
                // Normally there should be single volume per mount path
                .GroupBy(v => v.MountPath)
                .Where(g => g.Key != null)
                .ToDictionary(g => g.Key!.Value, g => g.First());
            var monitoredVolumes = configuration
                .MonitoringEntries
                .Select(d =>
                {
                    if (!fittingVolumesByMountPath.TryGetValue(d.MountPath, out var v))
                        v = null;
                    return new VolumeWithBob(
                        v,
                        d,
                        v != null ? configuration.KnownUuids.Contains(v.UUID) : false
                    );
                })
                .ToArray();
            return monitoredVolumes;
        }

        private record struct VolumeWithBob(Volume? Volume, BobDisk BobDisk, bool VolumeIsKnown)
        {
            public bool PhysicalExists => Volume != null;
        };
    }
}
