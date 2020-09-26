using System;
using System.Diagnostics;
using System.IO;

namespace DiskStatusAnalyzer.Rsync.Entities
{
    public class LocalEntry : IEquatable<LocalEntry>
    {
        private static readonly string rmCmd = @"C:\cygwin64\bin\rm.exe";
        public LocalEntry(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Remove()
        {
            using var process = new Process
            {
                StartInfo =  new ProcessStartInfo
                {
                    FileName = rmCmd,
                    Arguments = Path,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                }
            };
            process.Start();
            process.WaitForExit();
        }

        public bool Equals(LocalEntry other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Path == other.Path;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LocalEntry) obj);
        }

        public override int GetHashCode()
        {
            return (Path != null ? Path.GetHashCode() : 0);
        }
    }
}