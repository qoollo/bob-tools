using DisksMonitoring.OS.DisksProcessing.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    class Volume
    {
        public Volume(PhysicalId physicalId, DevPath devPath, UUID uUID, State state, MountPath? mountPath, Filesystem? fileSystem,
            IList<LogicalVolume> logicalVolumes, MountOptions? mountOptions)
        {
            PhysicalId = physicalId;
            DevPath = devPath;
            UUID = uUID;
            State = state;
            MountPath = mountPath;
            FileSystem = fileSystem;
            LogicalVolumes = logicalVolumes;
            MountOptions = mountOptions;
        }

        public PhysicalId PhysicalId { get; }
        public DevPath DevPath { get; }
        public UUID UUID { get; }
        public State State { get; }
        public MountPath? MountPath { get; }
        public Filesystem? FileSystem { get; }
        public IList<LogicalVolume> LogicalVolumes { get; }
        public MountOptions? MountOptions { get; }

        public bool IsFormatted => FileSystem != null || LogicalVolumes.Count != 0;
        public bool Mountable => LogicalVolumes.Count == 0;
        public bool IsMounted => State == State.Mounted && MountOptions?.IsRO != true;

        public bool ContainsPath(BobPath path)
        {
            return MountPath != null && path.StartsWith(MountPath.Value);
        }

        public override string ToString()
        {
            var res = new StringBuilder();
            res.Append($"[{PhysicalId}] {DevPath}");
            if (State == State.Mounted)
                res.Append($" mounted to {MountPath}");
            if (FileSystem != null)
                res.Append($" {FileSystem}");
            return res.ToString();
        }
    }
}
