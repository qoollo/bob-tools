using DisksMonitoring.Entities;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksProcessing;
using DisksMonitoring.OS.DisksProcessing.FSTabAltering;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.OS
{
    class DisksMonitor
    {
        private readonly DisksFinder disksFinder;
        private readonly DisksFormatter disksFormatter;
        private readonly DisksMounter disksMounter;
        private readonly FSTabAlterer fSTabAlterer;
        private readonly BobPathPreparer bobPathPreparer;
        private readonly NeededInfoStorage neededInfoStorage;
        private readonly ILogger<DisksMonitor> logger;

        public DisksMonitor(DisksFinder disksFinder, DisksFormatter disksFormatter, DisksMounter disksMounter, FSTabAlterer fSTabAlterer,
            BobPathPreparer bobPathPreparer, NeededInfoStorage neededInfoStorage, ILogger<DisksMonitor> logger)
        {
            this.disksFinder = disksFinder;
            this.disksFormatter = disksFormatter;
            this.disksMounter = disksMounter;
            this.fSTabAlterer = fSTabAlterer;
            this.bobPathPreparer = bobPathPreparer;
            this.neededInfoStorage = neededInfoStorage;
            this.logger = logger;
        }

        public async Task CheckAndUpdate()
        {
            await PrepareDisks();
            await FormatDisks();
            await MountDisks();
            await AlterFSTab();
        }

        public async Task<UUID> GetUUID(BobDisk info)
        {
            var disks = await disksFinder.FindDisks();
            var volume = disks.SelectMany(d => d.Volumes).Single(v => v.PhysicalId.Equals(info.PhysicalId));
            return volume.UUID;
        }

        private async Task MountDisks()
        {
            var disks = await FindDisks();
            foreach (var disk in disks)
            {
                foreach (var volume in disk.Volumes)
                {
                    if (neededInfoStorage.ShouldBeProcessed(volume))
                    {
                        await disksMounter.MountVolume(volume);
                        if (!neededInfoStorage.IsProtected(volume))
                        {
                            var bobPath = neededInfoStorage.FindBobPath(volume);
                            if (bobPath != null)
                                await bobPathPreparer.PrepareBobPath(bobPath);
                            else
                                logger.LogInformation($"No bobpath found for {volume}");
                        }
                    }
                }
            }
        }

        private async Task AlterFSTab()
        {
            var disks = await FindDisks();
            await fSTabAlterer.UpdateFSTabRecord(disks);
        }

        private async Task FormatDisks()
        {
            var disks = await FindDisks();

            foreach (var disk in disks)
                foreach (var volume in disk.Volumes)
                    if (neededInfoStorage.ShouldBeProcessed(volume) && !neededInfoStorage.IsProtected(volume))
                        await disksFormatter.Format(volume);
        }

        private async Task PrepareDisks()
        {
            var disks = await FindDisks();

            foreach (var disk in disks)
                if (!disk.Volumes.Any(v => neededInfoStorage.IsProtected(v)))
                    await disksFormatter.CreateVolume(disk);
        }

        private async Task<List<PhysicalDisk>> FindDisks()
        {
            var allDisks = await disksFinder.FindDisks();
            allDisks.RemoveAll(d => !neededInfoStorage.ShouldBeProcessed(d));
            logger.LogDisks(LogLevel.Debug, allDisks, "Needed disks");
            return allDisks;
        }
    }
}
