using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing.FileSystemAccessors
{
    public class LinuxFileSystemAccessor : IFileSystemAccessor
    {
        public string FindDevDiskPathTargetFile(string filename)
        {
            var info = new FileInfo(filename);
            try
            {
                return Path.GetFullPath(info.LinkTarget, Path.GetDirectoryName(filename));
            }
            catch { }
            return null;
        }

        public List<string> GetFilenames(string dir)
        {
            return Directory.GetFiles(dir).ToList();
        }
    }
}