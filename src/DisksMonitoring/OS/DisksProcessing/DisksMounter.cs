﻿using BobApi;
using DisksMonitoring.Config;
using DisksMonitoring.Exceptions;
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

        public async Task MountVolume(Volume volume, BobApiClient bobApiClient)
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
                if (!Directory.Exists(path))
                {
                    await processInvoker.InvokeSudoProcess("mkdir", path);
                }
                await TryCleanPreviousData(volume, bobApiClient, path);
                logger.LogInformation($"Mounting {volume} to {mountPath}");
                await processInvoker.InvokeSudoProcess("mount", volume.DevPath.Path, path);
                await processInvoker.SetDirPermissionsAndOwner(path, configuration.MountPointPermissions, configuration.MountPointOwner);
                logger.LogInformation($"Successfully mounted {volume} to {mountPath}");
            }
            else
                logger.LogInformation($"No mount path found for {volume}");
        }

        private async Task TryCleanPreviousData(Volume volume, BobApiClient bobApiClient, string path)
        {
            bool done = false;
            while (!done)
                try
                {
                    await TryStopBobdisk(volume, bobApiClient);
                    logger.LogInformation($"Trying to unmount previous disks in {path}");
                    await processInvoker.InvokeSudoProcess("umount", path);
                    done = true;
                    logger.LogInformation($"Successfully umounted previous disks in {path}");
                }
                catch (ProcessFailedException e) when (e.ExitCode == 32)
                {
                    await Task.Delay(1000);
                }
                catch
                {
                    done = true;
                }
        }

        private async Task TryStopBobdisk(Volume volume, BobApiClient bobApiClient)
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
                }
                catch (Exception e)
                {
                    logger.LogError($"Error stopping bobdisk: {e.Message}");
                }
            }
        }
    }
}
