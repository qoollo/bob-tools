using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public class NodeDiskRemoteDirsFinder
{
    private readonly ClusterExpandArguments _args;

    public NodeDiskRemoteDirsFinder(ClusterExpandArguments args)
    {
        _args = args;
    }

    public async Task<Dictionary<string, Dictionary<string, RemoteDir>>> FindRemoteDirByDiskByNode(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, Dictionary<string, RemoteDir>>();
        foreach (var node in config.Nodes)
        {
            var remoteDirByDisk = await GetRemoteDirByDisk(node, cancellationToken);
            result.Add(node.Name, remoteDirByDisk);
        }

        return result;
    }

    private async ValueTask<Dictionary<string, RemoteDir>> GetRemoteDirByDisk(
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        var addr = await node.FindIPAddress();
        var rootDir = await _args.GetRootDir(node, cancellationToken);
        return node.Disks.ToDictionary(
            d => d.Name,
            d => new RemoteDir(addr, Path.Combine(d.Path, rootDir))
        );
    }
}
