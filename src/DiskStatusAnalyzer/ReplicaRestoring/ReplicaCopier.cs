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
            var srcDisk = src.DiskDirs.Find(d => d.DiskName.Equals(srcDiskName));
            if (srcDisk == null)
            {
                logger.LogError($"Disk {srcDiskName} not found on source node {src.Uri} ({string.Join(", ", src.DiskDirs)})");
                return false;
            }

            var destDisk = dest.DiskDirs.Find(d => d.DiskName.Equals(destDiskName));
            if (destDisk == null)
            {
                logger.LogError($"Disk {destDiskName} not found on dest node {dest.Uri} ({string.Join(", ", dest.DiskDirs)})");
                return false;
            }

            return await rsyncWrapper.Copy(srcDisk, destDisk);
        }
    }
}
