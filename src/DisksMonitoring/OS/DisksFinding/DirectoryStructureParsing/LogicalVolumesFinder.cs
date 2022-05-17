using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing.FileSystemAccessors;
using DisksMonitoring.OS.DisksFinding.Entities;

namespace DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing
{
    public class LogicalVolumesFinder
    {
        private const string ByPathDir = "/dev/disk/by-path";
        private const string ByUuidDir = "/dev/disk/by-uuid";
        private readonly DevPathDataFinder _devPathDataFinder;

        public LogicalVolumesFinder(DevPathDataFinder devPathDataFinder)
        {
            _devPathDataFinder = devPathDataFinder;
        }

        public List<LogicalVolume> Find()
        {
            var physicalIdByDevPath = _devPathDataFinder.Find(ByPathDir, PhysicalId.FromString);
            var uuidByDevPath = _devPathDataFinder.Find(ByUuidDir, s => new UUID(s));
            var result = new List<LogicalVolume>();
            foreach (var (devPath, physId) in physicalIdByDevPath)
                if (uuidByDevPath.TryGetValue(devPath, out var uuid))
                    result.Add(new LogicalVolume(physId, devPath, uuid));
            return result;
        }
    }
}