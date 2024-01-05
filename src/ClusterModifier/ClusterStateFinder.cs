using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.Exceptions;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public class ClusterStateFinder
{
    private readonly INodeDiskRemoteDirsFinder _nodeDiskRemoteDirsFinder;
    private readonly IConfigurationsFinder _configurationsFinder;

    public ClusterStateFinder(
        INodeDiskRemoteDirsFinder nodeDiskRemoteDirsFinder,
        IConfigurationsFinder configurationsFinder
    )
    {
        _nodeDiskRemoteDirsFinder = nodeDiskRemoteDirsFinder;
        _configurationsFinder = configurationsFinder;
    }

    public async Task<ClusterState> Find(CancellationToken cancellationToken)
    {
        var oldConfig = await _configurationsFinder.FindOldConfig(cancellationToken);
        var config = await _configurationsFinder.FindNewConfig(cancellationToken);

        var vDiskInfo = await GetVDiskInfo(oldConfig, config, cancellationToken);
        var alienDirs = await GetAlienDirs(oldConfig, cancellationToken);
        return new ClusterState(vDiskInfo, alienDirs);
    }

    private async Task<List<VDiskInfo>> GetVDiskInfo(
        ClusterConfiguration oldConfig,
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var vDiskPairs = oldConfig
            .VDisks
            .Join(config.VDisks, vd => vd.Id, vd => vd.Id, (ovd, vd) => (ovd, vd));
        var findOldDir = await GetRootRemoteDirFinder(oldConfig, "old", cancellationToken);
        var findNewDir = await GetRootRemoteDirFinder(config, "new", cancellationToken);
        var result = new List<VDiskInfo>();
        foreach (var (oldVDisk, vDisk) in vDiskPairs)
        {
            var oldDirs = oldVDisk.Replicas.Select(r => findOldDir(r.Node, r.Disk, oldVDisk.Id));
            var newDirs = vDisk.Replicas.Select(r => findNewDir(r.Node, r.Disk, vDisk.Id));
            result.Add(new VDiskInfo(vDisk, oldDirs.ToArray(), newDirs.ToArray()));
        }
        return result;
    }

    private async Task<List<RemoteDir>> GetAlienDirs(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var remoteAlienDirByDiskByNode =
            await _nodeDiskRemoteDirsFinder.FindRemoteAlienDirByDiskByNode(
                config,
                cancellationToken
            );
        return remoteAlienDirByDiskByNode.Values.SelectMany(d => d.Values).ToList();
    }

    private async Task<Func<string, string, long, RemoteDir>> GetRootRemoteDirFinder(
        ClusterConfiguration config,
        string clusterConfigName,
        CancellationToken cancellationToken
    )
    {
        var remoteDirByDiskByNode = await _nodeDiskRemoteDirsFinder.FindRemoteRootDirByDiskByNode(
            config,
            cancellationToken
        );
        return (node, disk, vDiskId) =>
        {
            if (
                remoteDirByDiskByNode.TryGetValue(node, out var d)
                && d.TryGetValue(disk, out var rd)
            )
                return rd.GetSubdir(vDiskId.ToString());
            throw new ClusterStateException(
                $"Disk {disk} not found on node {node} in cluster config \"{clusterConfigName}\""
            );
        };
    }
}

public record struct ClusterState(List<VDiskInfo> VDiskInfo, List<RemoteDir> AlienDirs);

public record struct VDiskInfo(
    ClusterConfiguration.VDisk VDisk,
    RemoteDir[] OldDirs,
    RemoteDir[] NewDirs
);
