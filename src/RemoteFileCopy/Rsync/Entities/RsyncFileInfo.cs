using System.Net;
using System.Text.RegularExpressions;

namespace RemoteFileCopy.Rsync.Entities
{
    public class RsyncFileInfo
    {
        private static readonly Regex s_rsyncLine = new(@".*f""(?<f>.+)"".*l""(?<l>.+)"".*c""(?<c>.+)""");
        // private static readonly Regex s_rsyncLine = new(".*f\"(?<f>.+)\".*l\"(?<l>.+)\".*c\"(?<c>.+)\"");

        private RsyncFileInfo(IPAddress address, string filename, long lengthBytes, string checksum, RsyncFileInfoType type)
        {
            Address = address;
            Filename = filename;
            LengthBytes = lengthBytes;
            Checksum = checksum;
            Type = type;
        }

        public IPAddress Address { get; }
        public string Filename { get; }
        public long LengthBytes { get; }
        public string Checksum { get; }
        public RsyncFileInfoType Type { get; }

        public override string ToString()
        {
            return $"{Filename}"
                + (Type == RsyncFileInfoType.File ? $" ({LengthBytes}), checksum: {Checksum}" : "");
        }

        public static bool TryParseAbsolute(IPAddress address, string s, out RsyncFileInfo? fileInfo)
        {
            var match = s_rsyncLine.Match(s);
            if (match.Success && long.TryParse(match.Groups["l"].Value, out var length))
            {
                var filename = System.IO.Path.DirectorySeparatorChar + match.Groups["f"].Value;
                var checksum = match.Groups["c"].Value;
                if (!string.IsNullOrWhiteSpace(checksum))
                    fileInfo = new RsyncFileInfo(address, filename, length, checksum, RsyncFileInfoType.File);
                else
                    fileInfo = new RsyncFileInfo(address, filename, length, checksum, RsyncFileInfoType.Directory);
                return true;
            }

            fileInfo = null;
            return false;
        }
    }
}