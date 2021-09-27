using System.Collections.Generic;
using System.Linq;
using System.Text;
using BobAliensRecovery.AliensRecovery.Entities;
using BobApi.BobEntities;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;

namespace BobAliensRecovery.AliensRecovery
{
    public class ReplicasFinder
    {
        private readonly ILogger<ReplicasFinder> _logger;

        public ReplicasFinder(ILogger<ReplicasFinder> logger)
        {
            _logger = logger;
        }

        internal IEnumerable<Replicas> FindReplicas(ClusterConfiguration clusterConfiguration)
        {
            foreach (var vdisk in clusterConfiguration.VDisks)
            {
                var remoteDirByNodeName = GetRemoteDirByNodeName(clusterConfiguration, vdisk);
                yield return new Replicas(vdisk.Id, remoteDirByNodeName);
            }
        }

        private Dictionary<string, RemoteDir> GetRemoteDirByNodeName(ClusterConfiguration clusterConfiguration,
            ClusterConfiguration.VDisk vdisk)
        {
            var result = new Dictionary<string, RemoteDir>();
            var diskByName = vdisk.Replicas.ToDictionary(r => r.Node, r => r.Disk);
            foreach (var node in clusterConfiguration.Nodes)
            {
                if (diskByName.TryGetValue(node.Name, out var diskName))
                {
                    var disk = node.Disks.SingleOrDefault(d => d.Name == diskName);
                    if (disk != null)
                    {
                        var targetPath = System.IO.Path.Combine(disk.Path, "bob", vdisk.Id.ToString());
                        result.Add(node.Name, new RemoteDir(node.GetIPAddress(), targetPath));
                    }
                    else
                        _logger.LogError($"Disk {diskName} not found on node {node.Name}");
                }
            }

            return result;
        }
    }
}