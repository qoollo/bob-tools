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
        private readonly Configuration _configuration;
        private readonly ILogger<DisksCopier> _logger;
        private readonly IRemoteFileCopier _remoteFileCopier;

        public DisksCopier(
            Configuration configuration,
            ILogger<DisksCopier> logger,
            IRemoteFileCopier remoteFileCopier
        )
        {
            _configuration = configuration;
            _logger = logger;
            _remoteFileCopier = remoteFileCopier;
        }

        public async Task CopyDataFromReplica(
            ClusterConfiguration clusterConfiguration,
            BobApiClient bobApiClient,
            string nodeName,
            BobDisk bobDisk
        )
        {
            var localNode = clusterConfiguration.FindNodeByName(nodeName);
            var disk = localNode.FindDiskByPath(bobDisk.BobPath.Path);
            var targetVDisk = clusterConfiguration.FindVDiskByNodeNameDiskName(nodeName, disk.Name);
            if (targetVDisk == null)
            {
                return;
            }
            var restReplicas = targetVDisk
                .Replicas
                .Where(r => r.Node != nodeName || r.Disk != disk.Name);
            var currentDir = new RemoteDir(await localNode.FindIPAddress(), bobDisk.BobPath.Path);
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
                    _logger.LogInformation(
                        "Succesfully copied data for disk {Disk} from {From} to {To}",
                        bobDisk,
                        replicaRemoteDir,
                        currentDir
                    );
                    return;
                }
            }
            _logger.LogError("Failed to copy data to disk {Dir}", bobDisk);
        }
    }
}
