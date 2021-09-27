using System;
using BobApi.BobEntities;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class RestartOperation : IEquatable<RestartOperation>
    {
        public RestartOperation(ClusterConfiguration.Node node, string diskName)
        {
            Node = node;
            DiskName = diskName;
        }

        public ClusterConfiguration.Node Node { get; }
        public string DiskName { get; }

        public bool Equals(RestartOperation? other)
        {
            if (other is null)
                return false;

            return Node?.Name == other.Node.Name && DiskName == other.DiskName;
        }

        public override bool Equals(object? obj)
        {
            return obj is RestartOperation ro && Equals(ro);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Node.Name, DiskName);
        }
    }
}