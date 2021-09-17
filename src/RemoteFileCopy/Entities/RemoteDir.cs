using System.Net;

namespace RemoteFileCopy.Entities
{
    public class RemoteDir
    {
        public RemoteDir(IPAddress address, string path)
        {
            Address = address;
            Path = path;
        }

        public IPAddress Address { get; }
        public string Path { get; }

        public override string ToString()
        {
            return $"{Address}:{Path}";
        }
    }
}