using DisksMonitoring.Config;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksProcessing.Entities;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.OS.DisksProcessing
{
    class BobPathPreparer
    {
        private readonly ProcessInvoker processInvoker;
        private readonly Configuration configuration;
        private readonly ILogger<BobPathPreparer> logger;

        public BobPathPreparer(ProcessInvoker processInvoker, Configuration configuration, ILogger<BobPathPreparer> logger)
        {
            this.processInvoker = processInvoker;
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task PrepareBobPath(BobPath bobPath)
        {
            try
            {
                string path = bobPath.Path;
                if (Directory.Exists(path))
                {
                    var subDirs = Directory.GetDirectories(path);
                    foreach (var subdir in subDirs)
                    {
                        logger.LogInformation($"Removing {bobPath}");
                        Directory.Delete(path, true);
                    }
                }
                Directory.CreateDirectory(path);
                await processInvoker.SetDirPermissionsAndOwner(path, configuration.BobDirPermissions, configuration.BobDirOwner);
                logger.LogInformation($"Created {bobPath}");
            }
            catch (Exception)
            {
                logger.LogError($"Failed to prepare path {bobPath}");
                throw;
            }
        }
    }
}
