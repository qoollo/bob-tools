using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.DisksProcessing.FSTabAltering
{
    class FSTabId : IEquatable<FSTabId>
    {
        private readonly UUID uuid;
        private readonly DevPath? devPath;
        public FSTabId(string id)
        {
            if (id.StartsWith("UUID=", StringComparison.OrdinalIgnoreCase))
                uuid = new UUID(id.Substring(5));
            else if (id.StartsWith("/dev"))
                devPath = new DevPath(id);
        }

        public FSTabId(UUID uuid)
        {
            this.uuid = uuid;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FSTabId);
        }

        public bool Equals(FSTabId other)
        {
            return other != null &&
                   EqualityComparer<UUID>.Default.Equals(uuid, other.uuid) &&
                   EqualityComparer<DevPath?>.Default.Equals(devPath, other.devPath);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(uuid, devPath);
        }

        public bool IsForVolume(Volume volume)
        {
            if (uuid != null)
                return uuid.Equals(volume.UUID);
            else if (devPath != null)
                return devPath.Equals(volume.DevPath);
            return false;
        }

        public override string ToString()
        {
            if (uuid != null)
                return $"UUID={(string)uuid}";
            else if (devPath != null)
                return devPath?.Path;
            return null;
        }
    }
}
