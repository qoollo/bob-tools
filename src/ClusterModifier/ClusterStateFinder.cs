using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.Exceptions;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public class ClusterStateFinder
{
    private readonly ClusterExpandArguments _args;
    private readonly NodeDiskRemoteDirsFinder _nodeDirsRemoteDirsFinder;
    private readonly ILogger<ClusterStateFinder> _logger;

    public ClusterStateFinder(
        ClusterExpandArguments args,
        NodeDiskRemoteDirsFinder nodeDirsRemoteDirsFinder,
        ILogger<ClusterStateFinder> logger
    )
    {
        _args = args;
        _nodeDirsRemoteDirsFinder = nodeDirsRemoteDirsFinder;
        _logger = logger;
    }

    public async Task<ClusterState> Find(CancellationToken cancellationToken)
    {
        return new ClusterState(await GetVDiskInfo(cancellationToken));
    }

    private async Task<List<VDiskInfo>> GetVDiskInfo(CancellationToken cancellationToken)
    {
        var oldConfigResult = await _args.GetClusterConfigurationFromFile(
            _args.OldConfigPath,
            cancellationToken
        );
        if (!oldConfigResult.IsOk(out var oldConfig, out var oldError))
            throw new ConfigurationException($"Old config is not available: {oldError}");

        var configResult = await _args.FindClusterConfiguration(
            cancellationToken: cancellationToken
        );
        if (!configResult.IsOk(out var config, out var newError))
            throw new ConfigurationException($"Current config is not available: {newError}");

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
        var oldRemoteDirByDiskByNode = await _nodeDirsRemoteDirsFinder.FindRemoteDirByDiskByNode(
            oldConfig,
            cancellationToken
        );
        var newRemoteDirByDiskByNode = await _nodeDirsRemoteDirsFinder.FindRemoteDirByDiskByNode(
            config,
            cancellationToken
        );
        var result = new List<VDiskInfo>();
        RemoteDir GetOldDir(string node, string disk, long vDisk)
        {
            if (
                oldRemoteDirByDiskByNode.TryGetValue(node, out var d)
                && d.TryGetValue(disk, out var rd)
            )
                return rd.GetSubdir(vDisk.ToString());
            throw new ClusterStateException(
                $"Disk {disk} not found on node {node} in old cluster config"
            );
        }
        RemoteDir GetNewDir(string node, string disk, long vDisk)
        {
            if (
                newRemoteDirByDiskByNode.TryGetValue(node, out var d)
                && d.TryGetValue(disk, out var rd)
            )
                return rd.GetSubdir(vDisk.ToString());
            throw new ClusterStateException(
                $"Disk {disk} not found on node {node} in new cluster config"
            );
        }
        foreach (var (oldVDisk, vDisk) in vDiskPairs)
        {
            var oldDirs = oldVDisk.Replicas.Select(r => GetOldDir(r.Node, r.Disk, oldVDisk.Id));
            var newDirs = vDisk.Replicas.Select(r => GetNewDir(r.Node, r.Disk, vDisk.Id));
            result.Add(new VDiskInfo(vDisk, oldDirs.ToArray(), newDirs.ToArray()));
        }
        return result;
    }
}

public record struct ClusterState(List<VDiskInfo> VDiskInfo);

public record struct VDiskInfo(
    ClusterConfiguration.VDisk VDisk,
    RemoteDir[] OldDirs,
    RemoteDir[] NewDirs
);
