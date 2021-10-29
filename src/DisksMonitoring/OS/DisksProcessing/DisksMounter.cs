using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BobApi;
using DisksMonitoring.Config;
using DisksMonitoring.Exceptions;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;

namespace DisksMonitoring.OS.DisksProcessing
{
    class DisksMounter
    {
        private readonly ProcessInvoker processInvoker;
        private readonly ILogger<DisksMounter> logger;
        private readonly NeededInfoStorage neededInfoStorage;
        private readonly Configuration configuration;
        private readonly DisksFinder disksFinder;

        public DisksMounter(ProcessInvoker processInvoker, ILogger<DisksMounter> logger,
            NeededInfoStorage neededInfoStorage, Configuration configuration,
            DisksFinder disksFinder)
        {
            this.processInvoker = processInvoker;
            this.logger = logger;
            this.neededInfoStorage = neededInfoStorage;
            this.configuration = configuration;
            this.disksFinder = disksFinder;
        }

        public async Task MountVolume(Volume volume, BobApiClient bobApiClient)
        {
            if (volume.IsMounted && !volume.IsReadOnly)
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
                if (!Directory.Exists(path))
                {
                    await processInvoker.InvokeSudoProcess("mkdir", path);
                }
                if (await TryCleanPreviousData(volume, bobApiClient, mountPath.Value) && !volume.IsMounted)
                {
                    logger.LogInformation($"Mounting {volume} to {mountPath}");
                    await processInvoker.InvokeSudoProcess("mount", volume.DevPath.Path, path);
                    await processInvoker.SetDirPermissionsAndOwner(path, configuration.MountPointPermissions, configuration.MountPointOwner);
                    logger.LogInformation($"Successfully mounted {volume} to {mountPath}");
                }
            }
            else
                logger.LogInformation($"No mount path found for {volume}");
        }

        private async Task<bool> TryCleanPreviousData(Volume volume, BobApiClient bobApiClient, MountPath path)
        {
            int count = 0;
            await TryStopBobdisk(volume, bobApiClient);
            while (count++ < configuration.MaxUmountRetries)
                try
                {
                    var disks = await disksFinder.FindDisks();
                    if (disks.Any(d => d.Volumes.Count > 0 && d.Volumes.Any(v => v.MountPath?.Equals(path) == true && v.IsMounted)))
                    {
                        logger.LogInformation($"Trying to unmount previous disks in {path}");
                        await processInvoker.InvokeSudoProcess("umount", path.ToString());
                        logger.LogInformation($"Successfully umounted previous disks in {path}");
                    }
                    return true;
                }
                catch (ProcessFailedException e) when (e.ExitCode == 32)
                {
                    await Task.Delay(1000);
                }
                catch (Exception e)
                {
                    logger.LogError($"Error while unmounting previous disk: {e.Message}");
                    return false;
                }
            return false;
        }

        private async Task<bool> TryStopBobdisk(Volume volume, BobApiClient bobApiClient)
        {
            var bobDisk = neededInfoStorage.FindBobDisk(volume);
            if (bobDisk != null)
            {
                try
                {
                    logger.LogInformation($"Trying to stop bobdisk");
                    var stopResult = await bobApiClient.StopDisk(bobDisk.DiskNameInBob);
                    if (!stopResult.TryGetData(out var isStopped) || !isStopped)
                        logger.LogWarning($"Failed to stop bobdisk {bobDisk}");
                    else
                        logger.LogInformation($"Successfully stoped bobdisk {bobDisk}");
                    return true;
                }
                catch (Exception e)
                {
                    logger.LogError($"Error stopping bobdisk: {e.Message}");
                    return false;
                }
            }
            return true;
        }
    }
}
