using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RemoteFileCopy.Exceptions;
using RemoteFileCopy.Ssh.Entities;

namespace RemoteFileCopy.Rsync.Entities
{
    public class RsyncResult
    {
        private static readonly Regex s_totalSizeRegex = new(@"total size is (\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public RsyncResult(SshResult sshResult)
        {
            var totalSizeLine = sshResult.StdOut.FirstOrDefault(s_totalSizeRegex.IsMatch);
            if (totalSizeLine != null)
            {
                SyncedSize = long.Parse(s_totalSizeRegex.Match(totalSizeLine).Groups[1].Value);
            }
            else
                throw new CommandLineFailureException("rsync");

            var syncedFiles = new List<RsyncFileInfo>();
            foreach (var line in sshResult.StdOut)
                if (RsyncFileInfo.TryParseAbsolute(sshResult.Address, line, out var fileInfo) && fileInfo.Type == RsyncFileInfoType.File)
                    syncedFiles.Add(fileInfo);
            SyncedFiles = syncedFiles;
        }

        public long SyncedSize { get; }
        public bool IsError { get; }
        public IEnumerable<string> ErrorLines { get; } = Enumerable.Empty<string>();
        public IEnumerable<RsyncFileInfo> SyncedFiles { get; }
    }
}