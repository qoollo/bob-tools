using System.Linq;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using Microsoft.Extensions.Logging;
using RemoteFileCopy;
using RemoteFileCopy.Entities;

namespace DisksMonitoring.Bob
{
    class DisksCopier
    {
        private readonly Configuration configuration;
        private readonly ILogger<DisksCopier> logger;
        private readonly IRemoteFileCopier _remoteFileCopier;

        public DisksCopier(
            Configuration configuration,
            ILogger<DisksCopier> logger,
            IRemoteFileCopier remoteFileCopier
        )
        {
            this.configuration = configuration;
            this.logger = logger;
            _remoteFileCopier = remoteFileCopier;
        }

        public async Task CopyDataFromReplica(
            ClusterConfiguration clusterConfiguration,
            BobApiClient bobApiClient,
            string nodeName,
            BobDisk bobDisk
        )
        {
            var localNode = clusterConfiguration.Nodes.Single(n => n.Name == nodeName);
            var disk = localNode.Disks.Single(d => d.Path == bobDisk.BobPath.Path);
            var targetVDisk = clusterConfiguration
                .VDisks
                .SingleOrDefault(
                    vDisk => vDisk.Replicas.Any(r => r.Node == nodeName && r.Disk == disk.Name)
                );
            if (targetVDisk == null)
            {
                return;
            }
            var restReplicas = targetVDisk
                .Replicas
                .Where(r => r.Node != nodeName || r.Disk != disk.Name);
            var currentDir = new RemoteDir(System.Net.IPAddress.Loopback, bobDisk.BobPath.Path);
            foreach (var replica in restReplicas)
            {
                var replicaNode = clusterConfiguration.Nodes.Single(n => n.Name == replica.Node);
                var replicaDisk = replicaNode.Disks.Single(d => d.Name == replica.Disk);
                var replicaRemoteDir = new RemoteDir(
                    await replicaNode.FindIPAddress(),
                    replicaDisk.Path
                );

                var copyResult = await _remoteFileCopier.Copy(replicaRemoteDir, currentDir);
                if (copyResult.IsError == false)
                {
                    logger.LogInformation(
                        "Succesfully copied data for disk {Disk} from {From} to {To}",
                        bobDisk,
                        replicaRemoteDir,
                        currentDir
                    );
                    return;
                }
            }
            logger.LogError("Failed to copy data to disk {Dir}", bobDisk);
        }
    }
}
