using DisksMonitoring.Config;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.OS.DisksProcessing
{
    class DisksMounter
    {
        private readonly ProcessInvoker processInvoker;
        private readonly ILogger<DisksMounter> logger;
        private readonly NeededInfoStorage neededInfoStorage;
        private readonly Configuration configuration;

        public DisksMounter(ProcessInvoker processInvoker, ILogger<DisksMounter> logger, NeededInfoStorage neededInfoStorage, Configuration configuration)
        {
            this.processInvoker = processInvoker;
            this.logger = logger;
            this.neededInfoStorage = neededInfoStorage;
            this.configuration = configuration;
        }

        public async Task MountVolume(Volume volume)
        {
            if (volume.IsMounted)
            {
                logger.LogDebug($"{volume} is already mounted");
                return;
            }
            if (!volume.Mountable)
            {
                logger.LogDebug($"{volume} can't be mounted");
                return;
            }

            var mountPath = neededInfoStorage.FindMountPath(volume);
            if (mountPath != null)
            {
                var path = mountPath?.Path;
                logger.LogInformation($"Mounting {volume} to {mountPath}");
                if (!Directory.Exists(path))
                {
                    await processInvoker.InvokeSudoProcess("mkdir", path);
                }
                try
                {
                    logger.LogInformation($"Trying to unmount previous disks in {path}");
                    await processInvoker.InvokeSudoProcess("umount", path);
                    logger.LogInformation($"Successfully umounted previous disks in {path}");
                }
                catch { }
                await processInvoker.InvokeSudoProcess("mount", volume.DevPath.Path, path);
                await processInvoker.InvokeSudoProcess("chmod", configuration.MountPointPermissions, "-R", path);
                await processInvoker.InvokeSudoProcess("chown", configuration.MountPointOwner, "-R", path);
            }
            else
                logger.LogInformation($"No mount path found for {volume}");
        }
    }
}
