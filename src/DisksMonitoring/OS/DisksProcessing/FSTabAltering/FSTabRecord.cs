using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.DisksProcessing.FSTabAltering
{
    class FSTabRecord : IEquatable<FSTabRecord>
    {
        private FSTabRecord(string line)
        {
            var elems = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int addition = 0;
            if (elems.Length == 5)
                addition = 1;
            if (elems.Length + addition != 6)
                throw new ArgumentException($"Fstab line is in wrong format: {line}");

            Id = new FSTabId(elems[0]);
            if (elems[1] != "none")
                MountPath = new MountPath(elems[1]);
            else
                MountPath = null;
            Filesystem = new Filesystem(elems[2]);
            Options = addition == 1 ? "" : elems[3];
            Dump = int.Parse(elems[4 - addition]);
            Pass = int.Parse(elems[5 - addition]);
        }

        public FSTabRecord(Volume volume, string options)
        {
            Id = new FSTabId(volume.UUID);
            MountPath = volume.MountPath;
            Filesystem = volume.FileSystem.Value;
            Options = options ?? "";
            Dump = 0;
            Pass = 0;
        }

        public FSTabId Id { get; }
        public MountPath? MountPath { get; }
        public Filesystem Filesystem { get; }
        public string Options { get; set; }
        public int Dump { get; }
        public int Pass { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as FSTabRecord);
        }

        public bool Equals(FSTabRecord other)
        {
            return other != null &&
                   EqualityComparer<FSTabId>.Default.Equals(Id, other.Id) &&
                   EqualityComparer<MountPath?>.Default.Equals(MountPath, other.MountPath) &&
                   EqualityComparer<Filesystem>.Default.Equals(Filesystem, other.Filesystem) &&
                   Options == other.Options &&
                   Dump == other.Dump &&
                   Pass == other.Pass;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, MountPath, Filesystem, Options, Dump, Pass);
        }

        public override string ToString()
        {
            return $"{Id}\t{(MountPath is null ? "none" : MountPath?.Path)}\t{(string)Filesystem}\t{Options}\t{Dump}\t{Pass}";
        }

        public static bool TryParse(string line, out FSTabRecord record)
        {
            record = null;
            try
            {
                record = new FSTabRecord(line);
                return true;
            }
            catch
            {

            }
            return false;
        }
    }
}
