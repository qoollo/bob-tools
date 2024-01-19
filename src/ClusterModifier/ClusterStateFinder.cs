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
    private readonly IValidator _validator;

    public ClusterStateFinder(
        INodeDiskRemoteDirsFinder nodeDiskRemoteDirsFinder,
        IConfigurationsFinder configurationsFinder,
        IValidator validator
    )
    {
        _nodeDiskRemoteDirsFinder = nodeDiskRemoteDirsFinder;
        _configurationsFinder = configurationsFinder;
        _validator = validator;
    }

    public async Task<ClusterState> Find(CancellationToken cancellationToken)
    {
        var state = await GetState(cancellationToken);

        await _validator.Validate(state, cancellationToken);

        return state;
    }

    private async Task<ClusterState> GetState(CancellationToken cancellationToken)
    {
        var oldConfig = await _configurationsFinder.FindOldConfig(cancellationToken);
        var config = await _configurationsFinder.FindNewConfig(cancellationToken);
        ValidateConfigs(oldConfig, config);

        var vDiskInfo = await GetVDiskInfo(oldConfig, config, cancellationToken);
        var alienDirs = await GetAlienDirs(oldConfig, cancellationToken);
        var state = new ClusterState(vDiskInfo, alienDirs);
        return state;
    }

    private static void ValidateConfigs(ClusterConfiguration oldConfig, ClusterConfiguration config)
    {
        var oldVDiskIds = oldConfig.VDisks.Select(vd => vd.Id).ToHashSet();
        var newVDiskIds = config.VDisks.Select(vd => vd.Id).ToHashSet();
        oldVDiskIds.ExceptWith(newVDiskIds);
        if (oldVDiskIds.Count > 0)
            throw new ConfigurationException(
                $"Cluster shrink detection, removed vdisks {string.Join(", ", oldVDiskIds)}"
            );
    }

    private async Task<List<VDiskInfo>> GetVDiskInfo(
        ClusterConfiguration oldConfig,
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var findOldDirs = await GetVDiskRemoteDirsFinder(oldConfig, "old", cancellationToken);
        var findNewDirs = await GetVDiskRemoteDirsFinder(config, "new", cancellationToken);
        return oldConfig
            .VDisks.Join(
                config.VDisks,
                vd => vd.Id,
                vd => vd.Id,
                (ovd, vd) => new VDiskInfo(vd, findOldDirs(ovd), findNewDirs(vd))
            )
            .ToList();
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

    private async Task<Func<ClusterConfiguration.VDisk, RemoteDir[]>> GetVDiskRemoteDirsFinder(
        ClusterConfiguration config,
        string clusterConfigName,
        CancellationToken cancellationToken
    )
    {
        var remoteDirs = await _nodeDiskRemoteDirsFinder.FindRemoteRootDirByDiskByNode(
            config,
            cancellationToken
        );
        RemoteDir FindReplicaRemoteDir(
            ClusterConfiguration.VDisk v,
            ClusterConfiguration.VDisk.Replica r
        )
        {
            if (remoteDirs.TryGetValue(r.Node, out var d) && d.TryGetValue(r.Disk, out var rd))
                return rd.GetSubdir(v.Id.ToString());
            throw new ClusterStateException(
                $"Disk {r.Disk} not found on node {r.Node} in cluster config \"{clusterConfigName}\""
            );
        }
        return (vDisk) => vDisk.Replicas.Select(r => FindReplicaRemoteDir(vDisk, r)).ToArray();
    }
}

public record struct ClusterState(List<VDiskInfo> VDiskInfo, List<RemoteDir> AlienDirs);

public record struct VDiskInfo(
    ClusterConfiguration.VDisk VDisk,
    RemoteDir[] OldDirs,
    RemoteDir[] NewDirs
);
