using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Entities;
using System.IO;
using System;
using System.Security.Cryptography;
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
            _localAddresses = GetLocalAddresses();
        }

        public async Task<(bool isError, string[] files)> CopyWithRsync(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            if (TryGetLocalPath(from, out var fromPath) && TryGetLocalPath(to, out var toPath))
            {
                var files = await CopyFiles(fromPath, toPath, cancellationToken);
                return (false, files.ToArray());
            }
            else
                return await _remoteFileCopier.CopyWithRsync(from, to, cancellationToken);
        }

        public async Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            if (TryGetLocalPath(dir, out var path))
            {
                if (!Directory.Exists(path))
                    return false;
                foreach (var f in Directory.GetFiles(path))
                    File.Delete(f);
                foreach (var d in Directory.GetDirectories(path))
                    Directory.Delete(d);
                return true;
            }
            return await _remoteFileCopier.RemoveInDir(dir, cancellationToken);
        }

        public async Task<bool> RemoveDirectory(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            if (TryGetLocalPath(dir, out var path))
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path);
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
                if (!Directory.Exists(path))
                    return false;
                ClearAndCheckIsEmpty(path);
                return true;
            }
            return await _remoteFileCopier.RemoveEmptySubdirs(dir, cancellationToken);
        }

        public async Task RemoveAlreadyMovedFiles(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            if (TryGetLocalPath(from, out var fromPath) && TryGetLocalPath(to, out var toPath))
            {
                if (!Directory.Exists(fromPath))
                    return;
                foreach (var file in Directory.GetFiles(fromPath))
                {
                    var dest = Path.Combine(toPath, Path.GetFileName(fromPath));
                    if (File.Exists(dest) && GetCheckSum(file) == GetCheckSum(dest))
                    {
                        File.Delete(file);
                    }
                }
            }
            else
                await _remoteFileCopier.RemoveAlreadyMovedFiles(from, to, cancellationToken);
        }

        private bool TryGetLocalPath(RemoteDir dir, out string path)
        {
            path = dir.Path;
            return _localAddresses.Contains(dir.Address);
        }

        private async Task<List<string>> CopyFiles(string from, string to, CancellationToken cancellationToken)
        {
            var result = new List<string>();
            if (!Directory.Exists(to))
                Directory.CreateDirectory(to);

            foreach (var file in Directory.GetFiles(from))
            {
                var dest = Path.Combine(to, Path.GetFileName(from));
                using var open = File.Open(file, FileMode.Open);
                using var write = File.Create(dest);
                await open.CopyToAsync(write, cancellationToken);
                result.Add(file);
            }

            foreach (var dir in Directory.GetDirectories(from))
            {
                var destDir = Path.Combine(to, dir.Substring(from.Length));
                result.AddRange(await CopyFiles(dir, destDir, cancellationToken));
            }

            return result;
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
            return result && Directory.EnumerateFiles(path).Any();
        }

        private string GetCheckSum(string filePath)
        {
            using (var fileStream = File.OpenRead(filePath))
            return BitConverter.ToString(SHA256.Create().ComputeHash(fileStream)).Replace("-", "").ToLowerInvariant();
        }

        private static HashSet<IPAddress> GetLocalAddresses()
        {
            var result = new HashSet<IPAddress>();
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var ip in iface.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            result.Add(ip.Address);
                        }
                    }
                }
            }
            return result;
        }
    }
}