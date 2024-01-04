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
    private readonly INodeDiskRemoteDirsFinder _nodeDirsRemoteDirsFinder;
    private readonly IConfigurationsFinder _configurationsFinder;

    public ClusterStateFinder(
        INodeDiskRemoteDirsFinder nodeDirsRemoteDirsFinder,
        IConfigurationsFinder configurationsFinder
    )
    {
        _nodeDirsRemoteDirsFinder = nodeDirsRemoteDirsFinder;
        _configurationsFinder = configurationsFinder;
    }

    public async Task<ClusterState> Find(CancellationToken cancellationToken)
    {
        return new ClusterState(await GetVDiskInfo(cancellationToken));
    }

    private async Task<List<VDiskInfo>> GetVDiskInfo(CancellationToken cancellationToken)
    {
        var oldConfig = await _configurationsFinder.FindOldConfig(cancellationToken);
        var config = await _configurationsFinder.FindNewConfig(cancellationToken);

        return await GetVDiskInfo(oldConfig, config, cancellationToken);
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
        var findOldDir = await GetRemoteDirFinder(oldConfig, "old", cancellationToken);
        var findNewDir = await GetRemoteDirFinder(config, "new", cancellationToken);
        var result = new List<VDiskInfo>();
        foreach (var (oldVDisk, vDisk) in vDiskPairs)
        {
            var oldDirs = oldVDisk.Replicas.Select(r => findOldDir(r.Node, r.Disk, oldVDisk.Id));
            var newDirs = vDisk.Replicas.Select(r => findNewDir(r.Node, r.Disk, vDisk.Id));
            result.Add(new VDiskInfo(vDisk, oldDirs.ToArray(), newDirs.ToArray()));
        }
        return result;
    }

    private async Task<Func<string, string, long, RemoteDir>> GetRemoteDirFinder(
        ClusterConfiguration config,
        string clusterConfigName,
        CancellationToken cancellationToken
    )
    {
        var remoteDirByDiskByNode = await _nodeDirsRemoteDirsFinder.FindRemoteDirByDiskByNode(
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

public record struct ClusterState(List<VDiskInfo> VDiskInfo);

public record struct VDiskInfo(
    ClusterConfiguration.VDisk VDisk,
    RemoteDir[] OldDirs,
    RemoteDir[] NewDirs
);
