using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing.FileSystemAccessors;
using DisksMonitoring.OS.DisksFinding.Entities;

namespace DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing
{
    public class DevPathDataFinder
    {
        private const string ByPathDir = "/dev/disk/by-path";
        private const string ByUuidDir = "/dev/disk/by-uuid";
        private readonly IFileSystemAccessor _fileSystemAccessor;

        public DevPathDataFinder(IFileSystemAccessor fileSystemAccessor)
        {
            _fileSystemAccessor = fileSystemAccessor;
        }

        public Dictionary<DevPath, T> Find<T>(string dirname, Func<string, T> filenameProcessor)
        {
            var result = new Dictionary<DevPath, T>();
            var filenames = _fileSystemAccessor.GetFilenames(dirname);
            foreach (var filename in filenames)
            {
                var devPathString = _fileSystemAccessor.FindDevDiskPathTargetFile(filename);
                if (devPathString != null)
                {
                    var devPath = new DevPath(devPathString);
                    result.Add(devPath, filenameProcessor(Path.GetFileName(filename)));
                }
            }
            return result;
        }
    }
}