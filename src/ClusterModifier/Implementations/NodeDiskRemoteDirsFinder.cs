using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.Exceptions;
using BobToolsCli.Helpers;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public class NodeDiskRemoteDirsFinder : INodeDiskRemoteDirsFinder
{
    private readonly ClusterExpandArguments _args;

    public NodeDiskRemoteDirsFinder(ClusterExpandArguments args)
    {
        _args = args;
    }

    public async Task<
        Dictionary<string, Dictionary<string, RemoteDir>>
    > FindRemoteAlienDirByDiskByNode(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, Dictionary<string, RemoteDir>>();
        var prov = _args.GetBobApiClientProvider();
        foreach (var node in config.Nodes)
        {
            var dir = await FindAlienDir(prov, node, cancellationToken);
            // There is a possibility for multiple disks in future
            result.Add(node.Name, new Dictionary<string, RemoteDir> { ["alien"] = dir });
        }

        return result;
    }

    private static async Task<RemoteDir> FindAlienDir(
        BobApiClientProvider prov,
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        var client = prov.GetClient(node);
        var alienDir = await client.GetAlienDirectory(cancellationToken);
        RemoteDir dir;
        if (alienDir.IsOk(out var d, out var err))
        {
            var addr = await node.FindIPAddress();
            dir = new RemoteDir(addr, d.Path);
        }
        else
        {
            throw new ClusterStateException($"Failed to get alien dir for node {node.Name}: {err}");
        }

        return dir;
    }

    public async Task<
        Dictionary<string, Dictionary<string, RemoteDir>>
    > FindRemoteRootDirByDiskByNode(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, Dictionary<string, RemoteDir>>();
        foreach (var node in config.Nodes)
        {
            var remoteDirByDisk = await GetRemoteDirByDisk(node, cancellationToken);
            var rootDir = await _args.GetRootDir(node, cancellationToken);
            var remoteRootDirByDisk = remoteDirByDisk.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.GetSubdir(rootDir)
            );
            result.Add(node.Name, remoteRootDirByDisk);
        }

        return result;
    }

    private async ValueTask<Dictionary<string, RemoteDir>> GetRemoteDirByDisk(
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        var addr = await node.FindIPAddress();
        return node.Disks.ToDictionary(d => d.Name, d => new RemoteDir(addr, d.Path));
    }
}
