using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BobAliensRecovery.AliensRecovery.Entities;
using BobAliensRecovery.Exceptions;
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

        internal IDictionary<long, Replicas> FindReplicasByVdiskId(ClusterConfiguration clusterConfiguration)
        {
            var result = new Dictionary<long, Replicas>();
            foreach (var vdisk in clusterConfiguration.VDisks)
            {
                var remoteDirByNodeName = GetRemoteDirByNodeName(clusterConfiguration, vdisk);
                result.Add(vdisk.Id, new Replicas(vdisk.Id, remoteDirByNodeName));
            }
            return result;
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
                    {
                        throw new ConfigurationException("Configuration does not match running cluster");
                    }
                }
            }

            return result;
        }
    }
}