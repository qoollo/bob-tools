using System.Collections.Generic;

namespace DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing
{
    public interface IFileSystemAccessor
    {
        List<string> GetFilenames(string dir);
        string GetTargetFile(string filename);
    }
}