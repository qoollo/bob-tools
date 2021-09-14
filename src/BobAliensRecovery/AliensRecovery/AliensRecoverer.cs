using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.RemoteFileCreation;

namespace BobAliensRecovery.AliensRecovery
{
    public class AliensRecoverer
    {
        private readonly ILogger<AliensRecoverer> _logger;
        private readonly RemoteFileCreator _remoteFileCreator;

        public AliensRecoverer(ILogger<AliensRecoverer> logger,
            RemoteFileCreator remoteFileCreator)
        {
            _logger = logger;
            _remoteFileCreator = remoteFileCreator;
        }

        public async Task RecoverAliens(ClusterConfiguration clusterConfiguration,
            CancellationToken cancellationToken = default)
        {
            foreach (var node in clusterConfiguration.Nodes)
            {
                var ep = IPEndPoint.Parse(node.Address);
                foreach (var disk in node.Disks)
                {
                    await _remoteFileCreator.CreateRemoteFile(ep, disk.Path, cancellationToken);
                }
            }
        }
    }
}