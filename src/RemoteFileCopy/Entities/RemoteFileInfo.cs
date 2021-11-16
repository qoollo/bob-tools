using System.Net;

namespace RemoteFileCopy.Entities
{
    public class RemoteFileInfo
    {
        public RemoteFileInfo(IPAddress address, string filename, long lengthBytes, string checksum)
        {
            Address = address;
            Filename = filename;
            LengthBytes = lengthBytes;
            Checksum = checksum;
        }

        public IPAddress Address { get; }
        public string Filename { get; }
        public long LengthBytes { get; }
        public string Checksum { get; }

        public override string ToString()
        {
            return $"{Address}:{Filename} ({LengthBytes}) {Checksum}";
        }
    }
}