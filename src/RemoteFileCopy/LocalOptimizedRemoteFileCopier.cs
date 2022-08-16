using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;
using RemoteFileCopy.FilesFinding;
using RemoteFileCopy.Rsync;
using RemoteFileCopy.Rsync.Entities;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy
{
    public class LocalOptimizedRemoteFileCopier : IRemoteFileCopier
    {
        private readonly IRemoteFileCopier _remoteFileCopier;
        private readonly HashSet<IPAddress> _localAddresses;

        public LocalOptimizedRemoteFileCopier(IRemoteFileCopier remoteFileCopier)
        {
            _remoteFileCopier = remoteFileCopier;
            _localAddresses = new HashSet<IPAddress>();
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var ip in iface.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            _localAddresses.Add(ip.Address);
                        }
                    }
                }
            }
        }

        public async Task<RsyncResult> CopyWithRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.CopyWithRsync(from, to, cancellationToken);
        }

        public async Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.RemoveInDir(dir, cancellationToken);
        }

        public async Task<bool> RemoveFiles(IEnumerable<RemoteFileInfo> fileInfos, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.RemoveFiles(fileInfos, cancellationToken);
        }

        public async Task<bool> RemoveDirectory(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            if (TryGetLocalPath(dir, out var path))
            {
                if (System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.Delete(path);
                    return true;
                }
                return false;
            }
            return await _remoteFileCopier.RemoveDirectory(dir, cancellationToken);
        }

        public async Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            return await _remoteFileCopier.RemoveEmptySubdirs(dir, cancellationToken);
        }

        private bool TryGetLocalPath(RemoteDir dir, out string path)
        {
            path = dir.Path;
            return _localAddresses.Contains(dir.Address);
        }
    }
}
