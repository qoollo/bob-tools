using DisksMonitoring.Config;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.OS.DisksProcessing
{
    class DisksFormatter
    {
        private readonly Configuration configuration;
        private readonly ProcessInvoker processInvoker;
        private readonly ILogger<DisksFormatter> logger;

        public DisksFormatter(Configuration configuration, ProcessInvoker processInvoker, ILogger<DisksFormatter> logger)
        {
            this.configuration = configuration;
            this.processInvoker = processInvoker;
            this.logger = logger;
        }

        public async Task CreateVolume(PhysicalDisk physicalDisk)
        {
            if (physicalDisk.NoVolumes || !physicalDisk.Volumes.Any(v => v.IsMounted))
            {
                logger.LogInformation($"Creating primary volume on {physicalDisk}");
                await CreatePrimaryVolume(physicalDisk);
                logger.LogInformation($"Succeffully created primary volumd on {physicalDisk}");
            }
            else
                logger.LogInformation($"{physicalDisk} contains mounted volume");
        }

        public async Task Format(Volume volume)
        {
            if (!volume.IsMounted)
            {
                logger.LogInformation($"Formatting {volume}");
                await FormatPartiton(volume);
                logger.LogInformation($"Successfully formatted {volume} to {configuration.Filesystem}");
            }
            else
                logger.LogInformation($"{volume} is already formatted");
        }

        private async Task CreatePrimaryVolume(PhysicalDisk physicalDisk)
        {
            await processInvoker.InvokeSudoProcess("parted", physicalDisk.DevPath.Path, "--script", "--", "mklabel", "gpt");
            await processInvoker.InvokeSudoProcess("parted", physicalDisk.DevPath.Path, "--script", "--", "mkpart", "primary", "0%", "100%");
        }

        private async Task FormatPartiton(Volume volume)
        {
            await processInvoker.InvokeSudoProcess($"mkfs.{configuration.Filesystem}", volume.DevPath.Path);
        }
    }
}
