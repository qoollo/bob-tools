using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BobApi.Entities;

namespace DiskStatusAnalyzer.Rsync.Entities
{
    public class RsyncEntry
    {
        private static readonly Regex RsyncLineRegex =
            new Regex(@"(?<attrs>\S{10})\s*(?<size>[0-9,\.]+)\s*(?<date>\d+\S\d+\S\d+)\s*(?<time>\d+\S\d+\S\d+)\s*(?<name>\S+)");

        public RsyncEntry(ConnectionInfo configuration, Directory dir)
        {
            Name = dir.Name;
            Path = dir.Path;
            IsDirectory = true;
            ConInfo = configuration;
        }

        public RsyncEntry(RsyncEntry entry)
        {
            ConInfo = entry.ConInfo;
            Name = entry.Name;
            IsDirectory = entry.IsDirectory;
            Path = entry.Path;
        }

        public RsyncWrapper RsyncWrapper { get; }

        public string Name { get; }
        public bool IsDirectory { get; }
        public string Path { get; }
        public ConnectionInfo ConInfo { get; }
    }
}
