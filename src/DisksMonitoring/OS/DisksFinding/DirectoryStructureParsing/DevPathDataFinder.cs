using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing.FileSystemAccessors;
using DisksMonitoring.OS.DisksFinding.Entities;

namespace DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing
{
    public class DevPathDataFinder
    {
        private const string ByPathDir = "/dev/disk/by-path";
        private readonly IFileSystemAccessor _fileSystemAccessor;

        public DevPathDataFinder(IFileSystemAccessor fileSystemAccessor)
        {
            _fileSystemAccessor = fileSystemAccessor;
        }

        public Dictionary<DevPath, PhysicalId> FindPhysicalIdByDevPath() =>
            Find(ByPathDir, PhysicalId.FromString);

        private Dictionary<DevPath, T> Find<T>(string dirname, Func<string, T> filenameProcessor)
        {
            var result = new Dictionary<DevPath, string>();
            var filenames = _fileSystemAccessor.GetFilenames(dirname);
            foreach (var filename in filenames)
            {
                var devPathString = _fileSystemAccessor.FindDevDiskPathTargetFile(filename);
                if (devPathString != null)
                {
                    var devPath = new DevPath(devPathString);
                    var path = Path.GetFileName(filename);
                    if (!result.TryGetValue(devPath, out var p) || path.Length > p.Length)
                        result[devPath] = path;
                }
            }
            return result.ToDictionary(kv => kv.Key, kv => filenameProcessor(kv.Value));
        }
    }
}
