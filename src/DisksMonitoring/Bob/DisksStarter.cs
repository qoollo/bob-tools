using BobApi;
using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.Bob
{
    class DisksStarter
    {
        private readonly DisksMonitor disksMonitor;
        private readonly Configuration configuration;
        private readonly ILogger<DisksStarter> logger;

        public DisksStarter(DisksMonitor disksMonitor, Configuration configuration, ILogger<DisksStarter> logger)
        {
            this.disksMonitor = disksMonitor;
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task StartDisks(BobApiClient bobApiClient, List<BobDisk> deadInfo)
        {
            var newDead = await configuration.GetDeadInfo();
            foreach (var i in deadInfo.Except(newDead))
            {
                configuration.SaveUUID(await disksMonitor.GetUUID(i));
                logger.LogInformation($"Starting bobdisk {i}...");
                int retry = 0;
                while (!await bobApiClient.StartDisk(i.DiskNameInBob) && retry++ < configuration.StartRetryCount)
                    logger.LogWarning($"Failed to start bobdisk in try {retry}, trying again");
                if (retry == configuration.StartRetryCount)
                    logger.LogError($"Failed to start bobdisk {i}");
                else
                    logger.LogInformation($"Bobdisk {i} started");
            }
        }
    }
}
