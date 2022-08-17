using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Rsync.Entities;
using System.IO;
using System.Linq;

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
            if (TryGetLocalPath(from, out var fromPath) && TryGetLocalPath(to, out var toPath))
            {
                await CopyFiles(fromPath, toPath, cancellationToken);
            }
            return await _remoteFileCopier.CopyWithRsync(from, to, cancellationToken);
        }

        public async Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            if (TryGetLocalPath(dir, out var path))
            {
                foreach (var f in Directory.GetFiles(path))
                    File.Delete(f);
                foreach (var d in Directory.GetDirectories(path))
                    Directory.Delete(d);
                return true;
            }
            return await _remoteFileCopier.RemoveInDir(dir, cancellationToken);
        }

        public async Task<bool> RemoveFiles(IEnumerable<RemoteFileInfo> fileInfos, CancellationToken cancellationToken = default)
        {
            var local = fileInfos.Where(f => _localAddresses.Contains(f.Address)).ToArray();
            var remote = fileInfos.Except(local).ToArray();
            var result = true;
            if (local.Length > 0)
            {
                foreach(var f in local)
                {
                    var exists = File.Exists(f.Filename);
                    if (exists)
                        File.Delete(f.Filename);
                    else
                        result = false;
                }
            }
            if (remote.Length > 0)
                result &= await _remoteFileCopier.RemoveFiles(fileInfos, cancellationToken);
            return result;
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
            if (TryGetLocalPath(dir, out var path))
            {
                ClearAndCheckIsEmpty(path);
                return true;
            }
            return await _remoteFileCopier.RemoveEmptySubdirs(dir, cancellationToken);
        }

        private bool TryGetLocalPath(RemoteDir dir, out string path)
        {
            path = dir.Path;
            return _localAddresses.Contains(dir.Address);
        }

        private async Task<object> CopyFiles(string from, string to, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(to))
                Directory.CreateDirectory(to);

            foreach (var file in Directory.GetFiles(from))
            {
                var dest = file.Replace(from, to);
                using var open = File.Open(file, FileMode.Open);
                using var write = File.Create(dest);
                await open.CopyToAsync(write, cancellationToken);
            }

            foreach (var dir in Directory.GetDirectories(from))
            {
                await CopyFiles(dir, dir.Replace(from, to), cancellationToken);
            }

            return new object();
        }

        private bool ClearAndCheckIsEmpty(string path)
        {
            var result = true;
            foreach (var dir in Directory.GetDirectories(path))
            {
                if (ClearAndCheckIsEmpty(dir))
                    Directory.Delete(dir);
                else
                    result = false;
            }
            return result && Directory.GetFiles(path).Length == 0;
        }
    }
}
