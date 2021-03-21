using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.Rsync;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiskStatusAnalyzer.ReplicaRestoring
{
    class ReplicaCopier
    {
        private readonly ILogger<ReplicaCopier> logger;
        private readonly RsyncWrapper rsyncWrapper;

        public ReplicaCopier(ILogger<ReplicaCopier> logger, RsyncWrapper rsyncWrapper)
        {
            this.logger = logger;
            this.rsyncWrapper = rsyncWrapper;
        }

        public async Task<bool> Copy(NodeWithDirs src, string srcDiskName, NodeWithDirs dest, string destDiskName)
        {
            var srcDisk = src.DiskDirs.Find(d => d.Name.Equals(srcDiskName));
            if (srcDisk == null)
            {
                logger.LogError($"Disk {srcDiskName} not found on source node {src.Uri}");
                return false;
            }

            var destDisk = dest.DiskDirs.Find(d => d.Name.Equals(destDiskName));
            if (destDisk == null)
            {
                logger.LogError($"Disk {destDiskName} not found on dest node {dest.Uri}");
                return false;
            }

            return await rsyncWrapper.Copy(srcDisk, destDisk);
        }
    }
}
