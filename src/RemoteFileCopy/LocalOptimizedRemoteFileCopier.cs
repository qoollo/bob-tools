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

        public async Task<CopyResult> Copy(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetLocalPath(from, out var fromPath) && TryGetLocalPath(to, out var toPath))
            {
                var files = CopyFiles(fromPath, toPath, cancellationToken);
                return new CopyResult(false, files.ToArray());
            }
            else
                return await _remoteFileCopier.Copy(from, to, cancellationToken);
        }

        public async Task<bool> RemoveInDir(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetLocalPath(dir, out var path))
            {
                if (!Directory.Exists(path))
                    return false;
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    File.Delete(f);
                ClearAndCheckIsEmpty(path);
                return true;
            }
            return await RemoveEmptySubdirs(dir, cancellationToken);
        }

        public async Task<bool> RemoveDirectory(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetLocalPath(dir, out var path))
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    return true;
                }
                return false;
            }
            return await _remoteFileCopier.RemoveDirectory(dir, cancellationToken);
        }

        public async Task<bool> RemoveEmptySubdirs(RemoteDir dir, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetLocalPath(dir, out var path))
            {
                if (!Directory.Exists(path))
                    return false;
                ClearAndCheckIsEmpty(path);
                return true;
            }
            return await _remoteFileCopier.RemoveEmptySubdirs(dir, cancellationToken);
        }

        public async Task<int> RemoveAlreadyMovedFiles(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetLocalPath(from, out var fromPath) && TryGetLocalPath(to, out var toPath))
            {
                return RemoveLocalAlreadyMovedFiles(fromPath, toPath, cancellationToken);
            }
            else
                return await _remoteFileCopier.RemoveAlreadyMovedFiles(from, to, cancellationToken);
        }

        public async Task<bool> SourceCopiedToDest(RemoteDir from, RemoteDir to, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetLocalPath(from, out var fromPath) && TryGetLocalPath(to, out var toPath))
            {
                return CheckContainsSameFiles(fromPath, toPath, cancellationToken);
            }
            else
                return await _remoteFileCopier.SourceCopiedToDest(from, to, cancellationToken);
        }

        private bool CheckContainsSameFiles(string fromPath, string toPath,
                                            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(fromPath) || !Directory.Exists(toPath))
                return false;
            foreach (var file in Directory.GetFiles(fromPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dest = Path.Combine(toPath, Path.GetFileName(file));
                if (!File.Exists(dest)
                    || GetFileSize(file) != GetFileSize(dest)
                    || GetCheckSum(file) != GetCheckSum(dest))
                {
                    return false;
                }
            }
            foreach(var dir in Directory.GetDirectories(fromPath))
            {
                var toDir = Path.Combine(toPath, dir.Substring(fromPath.Length + 1));
                if (!CheckContainsSameFiles(dir, toDir, cancellationToken))
                    return false;
            }
            return true;
        }

        private int RemoveLocalAlreadyMovedFiles(string fromPath, string toPath,
                                            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(fromPath) || !Directory.Exists(toPath))
                return 0;
            int count = 0;
            foreach (var file in Directory.GetFiles(fromPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dest = Path.Combine(toPath, Path.GetFileName(file));
                if (File.Exists(dest)
                    && GetFileSize(file) == GetFileSize(dest)
                    && GetCheckSum(file) == GetCheckSum(dest))
                {
                    File.Delete(file);
                    count++;
                }
            }
            foreach(var dir in Directory.GetDirectories(fromPath))
            {
                var toDir = Path.Combine(toPath, dir.Substring(fromPath.Length + 1));
                count += RemoveLocalAlreadyMovedFiles(dir, toDir, cancellationToken);
            }
            return count;
        }

        private bool TryGetLocalPath(RemoteDir dir, out string path)
        {
            path = dir.Path;
            return _localAddresses.Contains(dir.Address);
        }

        private List<string> CopyFiles(string from, string to, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new List<string>();
            if (!Directory.Exists(to))
                Directory.CreateDirectory(to);

            foreach (var file in Directory.GetFiles(from))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dest = Path.Combine(to, Path.GetFileName(file));
                File.Copy(file, dest, true);
                result.Add(file);
            }

            foreach (var dir in Directory.GetDirectories(from))
            {
                var destDir = Path.Combine(to, dir.Substring(from.Length + 1));
                result.AddRange(CopyFiles(dir, destDir, cancellationToken));
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
            return result && !Directory.EnumerateFiles(path).Any();
        }

        private long GetFileSize(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }

        private string GetCheckSum(string filePath)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                var hash = SHA256.Create().ComputeHash(fileStream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
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
