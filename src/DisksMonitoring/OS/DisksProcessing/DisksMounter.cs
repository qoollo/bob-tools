using System;
using System.Collections.Generic;
using System.IO;
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

        public DisksMounter(ProcessInvoker processInvoker, ILogger<DisksMounter> logger, NeededInfoStorage neededInfoStorage, Configuration configuration)
        {
            this.processInvoker = processInvoker;
            this.logger = logger;
            this.neededInfoStorage = neededInfoStorage;
            this.configuration = configuration;
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
                if (await TryCleanPreviousData(volume, bobApiClient, path))
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

        private async Task<bool> TryCleanPreviousData(Volume volume, BobApiClient bobApiClient, string path)
        {
            int count = 0;
            if (await TryStopBobdisk(volume, bobApiClient))
                while (count++ < configuration.MaxUmountRetries)
                    try
                    {
                        logger.LogInformation($"Trying to unmount previous disks in {path}");
                        await processInvoker.InvokeSudoProcess("umount", path);
                        logger.LogInformation($"Successfully umounted previous disks in {path}");
                        return true;
                    }
                    catch (ProcessFailedException e) when (e.ExitCode == 32)
                    {
                        await Task.Delay(1000);
                    }
                    catch(Exception e)
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
                    if (!await bobApiClient.StopDisk(bobDisk.DiskNameInBob))
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
