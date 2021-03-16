using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksProcessing.Entities;
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
        private readonly ILogger<BobPathPreparer> logger;

        public BobPathPreparer(ILogger<BobPathPreparer> logger)
        {
            this.logger = logger;
        }

        public void PrepareBobPath(BobPath bobPath)
        {
            try
            {
                string path = bobPath.Path;
                if (Directory.Exists(path))
                {
                    logger.LogInformation($"Removing {bobPath}");
                    Directory.Delete(path, true);
                }
                Directory.CreateDirectory(path);
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
