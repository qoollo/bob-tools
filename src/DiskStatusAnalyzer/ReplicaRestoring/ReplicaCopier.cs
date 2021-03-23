using DiskStatusAnalyzer.Entities;
using DiskStatusAnalyzer.Rsync;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<bool> Copy(NodeWithDirs src, NodeWithDirs dest, int vdiskId)
        {
            var srcDisk = src.DiskDirs.Find(d => d.Bob.VDisks.Any(vd => vd.Id == vdiskId));
            if (srcDisk == null)
            {
                logger.LogError($"VDisk {vdiskId} not found on source node {src.Uri} ({string.Join(", ", src.DiskDirs)})");
                return false;
            }

            var destDisk = dest.DiskDirs.Find(d => d.Bob.VDisks.Any(vd => vd.Id == vdiskId));
            if (destDisk == null)
            {
                logger.LogError($"Disk {vdiskId} not found on dest node {dest.Uri} ({string.Join(", ", dest.DiskDirs)})");
                return false;
            }

            return await rsyncWrapper.Copy(srcDisk.Bob.VDisks.Single(vd => vd.Id == vdiskId), destDisk.Bob.VDisks.Single(vd => vd.Id == vdiskId));
        }
    }
}
