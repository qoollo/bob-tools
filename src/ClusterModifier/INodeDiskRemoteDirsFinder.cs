using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public interface INodeDiskRemoteDirsFinder
{
    Task<Dictionary<string, Dictionary<string, RemoteDir>>> FindRemoteRootDirByDiskByNode(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    );
    Task<Dictionary<string, Dictionary<string, RemoteDir>>> FindRemoteAlienDirByDiskByNode(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    );
}
