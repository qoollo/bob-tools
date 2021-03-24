using BobApi;
using BobApi.Entities;
using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.Bob
{
    class DisksCopier
    {
        private readonly Configuration configuration;
        private readonly ProcessInvoker processInvoker;
        private readonly ILogger<DisksCopier> logger;

        public DisksCopier(Configuration configuration, ProcessInvoker processInvoker, ILogger<DisksCopier> logger)
        {
            this.configuration = configuration;
            this.processInvoker = processInvoker;
            this.logger = logger;
        }

        public async Task CopyDataFromReplica(BobApiClient bobApiClient, BobDisk bobDisk)
        {
            if (configuration.PathToDiskStatusAnalyzer == null || !File.Exists(configuration.PathToDiskStatusAnalyzer))
            {
                logger.LogInformation($"DiskStatusAnalyzer path ({configuration.PathToDiskStatusAnalyzer}) is invalid, skipping copy");
                return;
            }
            var status = await bobApiClient.GetStatus();
            if (status is null)
            {
                logger.LogError($"Failed to get status from {bobApiClient}");
                return;
            }
            var destName = status?.Name;
            var diskName = bobDisk.DiskNameInBob;
            bool IsCurrent(Replica replica) => replica.Node == destName && replica.Disk == diskName;
            var vdisks = status?.VDisks.Where(vd => vd.Replicas.Any(IsCurrent));
            if (!vdisks.Any())
            {
                logger.LogError($"VDisks with replica ({diskName}, {destName}) not found");
                return;
            }
            foreach (var vdisk in vdisks)
            {
                string vdiskPath = Path.Combine(bobDisk.BobPath.Path, "bob", vdisk.Id.ToString());
                try
                {
                    if (!System.IO.Directory.Exists(vdiskPath))
                    {
                        logger.LogInformation($"Creating vdisk dir {vdiskPath}");
                        System.IO.Directory.CreateDirectory(vdiskPath);
                    }
                    else
                        logger.LogDebug($"Directory for vdisk {vdiskPath} already exists");
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to create vdisk dir {vdiskPath}: {e.Message}");
                }
                foreach (var replica in vdisk.Replicas)
                {
                    logger.LogInformation($"Trying to copy {vdisk} from {replica.Node} to {destName}");
                    try
                    {
                        await PerformCopy(replica.Node, destName, vdisk.Id);
                        logger.LogInformation($"Successfully copied {vdisk} from {replica.Node} to {destName}");
                        break;
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Failed to copy {vdisk} from {replica.Node} to {destName}: {e.Message}");
                    }
                }
            }
        }

        private async Task PerformCopy(string sourceName, string destName, int vdiskId)
        {
            string fullPath = Path.GetFullPath(configuration.PathToDiskStatusAnalyzer);
            await processInvoker.InvokeSudoProcessWithWD(fullPath, Path.GetDirectoryName(fullPath),
                "copy-vdisk", $"-s {sourceName}", $"-d {destName}", $"-v {vdiskId}");
        }
    }
}
