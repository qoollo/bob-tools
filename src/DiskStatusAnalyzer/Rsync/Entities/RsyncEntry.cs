using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiskStatusAnalyzer.Rsync.Entities
{
    public class RsyncEntry : IEquatable<RsyncEntry>
    {
        private static readonly Regex RsyncLineRegex = 
            new Regex(@"(?<attrs>\S{10})\s*(?<size>[0-9,\.]+)\s*(?<date>\d+\S\d+\S\d+)\s*(?<time>\d+\S\d+\S\d+)\s*(?<name>\S+)");

        public RsyncEntry(string rsyncDirLine, string parentPath, RsyncWrapper rsyncWrapper)
        {
            this.RsyncWrapper = rsyncWrapper;
            var match = RsyncLineRegex.Match(rsyncDirLine);
            if (!match.Success)
                throw new ArgumentException("Line is not in correct format");
            Name = match.Groups["name"].Value;
            IsDirectory = match.Groups["attrs"].Value[0] == 'd';
            Path = $"{parentPath}/{Name}";
        }

        public RsyncEntry(RsyncEntry entry)
        {
            RsyncWrapper = entry.RsyncWrapper;
            Name = entry.Name;
            IsDirectory = entry.IsDirectory;
            Path = entry.Path;
        }

        public RsyncWrapper RsyncWrapper { get; }

        public Task<List<string>> FindFilesWithSha()
        {
            var command = RsyncWrapper.GetListFilesWithShaCommand(Path);
            return RsyncWrapper.InvokeSshCommandWithOutput(command);
        }

        public Task<List<string>> ReadSyncedFiles()
        {
            var command = RsyncWrapper.GetSyncedFilesReadCommand(Path);
            return RsyncWrapper.InvokeSshCommandWithOutput(command);
        }

        public Task<bool> RemoveFiles(IEnumerable<string> filenames)
        {
            var command = RsyncWrapper.GetRemoveFilesCommand(filenames);
            return RsyncWrapper.InvokeSshCommand(command);
        }

        public string Name { get; }
        public bool IsDirectory { get; }
        public string Path { get; }

        public Task<bool> CopyTo(RsyncEntry to)
        {
            return RsyncWrapper.Copy(this, to);
        }
        
        public bool Equals(RsyncEntry other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(RsyncWrapper, other.RsyncWrapper) && Name == other.Name && IsDirectory == other.IsDirectory && Path == other.Path;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RsyncEntry) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RsyncWrapper, Name, IsDirectory, Path);
        }
    }
}