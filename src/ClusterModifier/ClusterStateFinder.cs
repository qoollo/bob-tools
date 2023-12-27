using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly ILogger<ClusterStateFinder> _logger;

    public ClusterStateFinder(ClusterExpandArguments args, ILogger<ClusterStateFinder> logger)
    {
        _args = args;
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
        var vDiskPairs = oldConfig.VDisks.Join(
            config.VDisks,
            vd => vd.Id,
            vd => vd.Id,
            (ovd, vd) => (ovd, vd)
        );
        var oldNodeInfoByName = await GetNodeInfoByName(oldConfig, cancellationToken);
        var nodeInfoByName = await GetNodeInfoByName(config, cancellationToken);
        var result = new List<VDiskInfo>();
        foreach (var (oldVDisk, vDisk) in vDiskPairs)
        {
            var oldDirs = oldVDisk.Replicas.Select(
                r => oldNodeInfoByName[r.Node].GetRemoteDirForDisk(r.Disk, oldVDisk)
            );
            var newDirs = vDisk.Replicas.Select(
                r => nodeInfoByName[r.Node].GetRemoteDirForDisk(r.Disk, vDisk)
            );
            result.Add(new VDiskInfo(vDisk, oldDirs.ToArray(), newDirs.ToArray()));
        }
        return result;
    }

    private async Task<Dictionary<string, NodeInfo>> GetNodeInfoByName(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var nodeInfoByName = new Dictionary<string, NodeInfo>();
        foreach (var node in config.Nodes)
        {
            var cr = await GetCreator(node, cancellationToken);
            var disks = node.Disks.ToDictionary(d => d.Name);
            nodeInfoByName.Add(node.Name, new NodeInfo(node, cr, disks));
        }

        return nodeInfoByName;
    }

    private delegate RemoteDir GetRemoteDir(
        ClusterConfiguration.Node.Disk disk,
        ClusterConfiguration.VDisk vDisk
    );

    private async ValueTask<GetRemoteDir> GetCreator(
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken = default
    )
    {
        var addr = await node.FindIPAddress();
        var rootDir = await _args.GetRootDir(node, cancellationToken);
        return (disk, vdisk) =>
            new RemoteDir(addr, Path.Combine(disk.Path, rootDir, vdisk.Id.ToString()));
    }

    private readonly struct NodeInfo
    {
        private readonly GetRemoteDir _getRemoteDir;
        private readonly Dictionary<string, ClusterConfiguration.Node.Disk> _diskByName;
        private readonly ClusterConfiguration.Node _node;

        public NodeInfo(
            ClusterConfiguration.Node node,
            GetRemoteDir getRemoteDir,
            Dictionary<string, ClusterConfiguration.Node.Disk> diskByName
        )
        {
            _node = node;
            _getRemoteDir = getRemoteDir;
            _diskByName = diskByName;
        }

        public RemoteDir GetRemoteDirForDisk(string diskName, ClusterConfiguration.VDisk vDisk) =>
            _diskByName.TryGetValue(diskName, out var disk)
                ? _getRemoteDir(disk, vDisk)
                : throw new ClusterStateException(
                    $"Disk {diskName} not found on node {_node.Name}"
                );
    }
}

public record struct ClusterState(List<VDiskInfo> VDiskInfo);

public record struct VDiskInfo(
    ClusterConfiguration.VDisk VDisk,
    RemoteDir[] OldDirs,
    RemoteDir[] NewDirs
);
