using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RemoteFileCopy.Exceptions;
using RemoteFileCopy.Ssh.Entities;

namespace RemoteFileCopy.Rsync.Entities
{
    public class RsyncResult
    {
        private static readonly Regex s_totalSizeRegex = new(@"total size is ([\d,]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_sentRegex = new(@"sent ([\d,]+) bytes",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_receivedRegex = new(@"received ([\d,]+) bytes",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public RsyncResult(SshResult sshResult)
        {
            SyncedSize = GetSize(sshResult.StdOut, s_totalSizeRegex);
            SentSize = GetSize(sshResult.StdOut, s_sentRegex);
            ReceivedSize = GetSize(sshResult.StdOut, s_receivedRegex);

            var syncedFiles = new List<RsyncFileInfo>();
            foreach (var line in sshResult.StdOut)
                if (RsyncFileInfo.TryParseAbsolute(sshResult.Address, line, out var fileInfo) && fileInfo.Type == RsyncFileInfoType.File)
                    syncedFiles.Add(fileInfo);
            SyncedFiles = syncedFiles;
        }

        public long SyncedSize { get; }
        public long SentSize { get; }
        public long ReceivedSize { get; }
        public bool IsError { get; }
        public IEnumerable<string> ErrorLines { get; } = Enumerable.Empty<string>();
        public IEnumerable<RsyncFileInfo> SyncedFiles { get; }

        public override string ToString()
        {
            return $"Sent {SentSize} bytes, received {ReceivedSize}, synced {SyncedSize}";
        }

        private static long GetSize(IEnumerable<string> lines, Regex sizeRegex)
        {
            var sizeLine = lines.FirstOrDefault(sizeRegex.IsMatch);
            if (sizeLine != null)
            {
                return long.Parse(sizeRegex.Match(sizeLine).Groups[1].Value,
                    System.Globalization.NumberStyles.Any);
            }
            else
                throw new CommandLineFailureException("rsync");
        }
    }
}