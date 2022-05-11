using System.Collections.Generic;
using DisksMonitoring.OS.DisksFinding.Entities;

namespace DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing
{
    public class PhysicalIdFinder
    {
        private const string PathDir = "/dev/disk/by-path";
        private readonly IFileSystemAccessor _fileSystemAccessor;

        public PhysicalIdFinder(IFileSystemAccessor fileSystemAccessor)
        {
            _fileSystemAccessor = fileSystemAccessor;
        }

        public Dictionary<DevPath, PhysicalId> Find()
        {
            var result = new Dictionary<DevPath, PhysicalId>();
            var filenames = _fileSystemAccessor.GetFilenames(PathDir);
            foreach (var filename in filenames)
            {
                var split = filename.Split('-');
                var port = "";
                var count = -1;
                while (split.Length - count > 2)
                {
                    count += 2;
                    port = split[count];
                }
                var devPathString = _fileSystemAccessor.GetTargetFile(filename);
                var devPath = new DevPath(devPathString);
                result.Add(devPath, PhysicalId.FromString(port));
            }
            return result;
        }
    }
}