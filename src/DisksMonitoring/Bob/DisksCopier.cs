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
                logger.LogInformation($"DiskStatusAnalyzer path is not set or invalid, skipping copy");
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
            var vdisk = status?.VDisks.Find(vd => vd.Replicas.Any(IsCurrent));
            if (vdisk is null)
            {
                logger.LogError($"VDisk with replica ({diskName}, {destName}) not found");
                return;
            }
            foreach (var replica in vdisk?.Replicas)
            {
                logger.LogInformation($"Trying to copy from {replica.Node} to {destName}");
                try
                {
                    await PerformCopy(replica.Node, destName, diskName);
                    logger.LogInformation($"Successfully copied data from {replica.Node} to {destName}");
                    return;
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to copy from {replica.Node} to {destName}: {e.Message}");
                }
            }
        }

        private async Task PerformCopy(string sourceName, string destName, string diskName)
        {
            await processInvoker.InvokeSudoProcess(configuration.PathToDiskStatusAnalyzer, $"-s {sourceName}", $"-d {destName}", $"-n {diskName}");
        }
    }
}
