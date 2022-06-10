using System.Collections.Generic;

namespace DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing.FileSystemAccessors
{
    public interface IFileSystemAccessor
    {
        List<string> GetFilenames(string dir);
        string FindDevDiskPathTargetFile(string filename);
    }
}