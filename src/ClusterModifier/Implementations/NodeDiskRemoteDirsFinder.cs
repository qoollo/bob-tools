using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
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
