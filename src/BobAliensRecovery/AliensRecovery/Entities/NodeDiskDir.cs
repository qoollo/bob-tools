using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BobApi.BobEntities;
using BobApi.Entities;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class NodeDiskDir : IEquatable<NodeDiskDir>
    {
        public NodeDiskDir(ClusterConfiguration.Node node, string diskName, Directory directory)
        {
            Node = node;
            DiskName = diskName;
            Directory = directory;
            Children = Directory.Children.Select(c => new NodeDiskDir(node, diskName, c)).ToArray();
        }

        public ClusterConfiguration.Node Node { get; }
        public string DiskName { get; }
        public Directory Directory { get; }
        public IEnumerable<NodeDiskDir> Children { get; }

        public IEnumerable<NodeDiskDir> GetMatchedDirs(Func<NodeDiskDir, bool> predicate)
        {
            if (predicate(this))
                yield return this;
            foreach (var child in Children)
                foreach (var match in child.GetMatchedDirs(predicate))
                    yield return match;
        }

        public bool Equals(NodeDiskDir? other)
        {
            if (other is null)
                return false;

            return Node.Name == other.Node.Name
                && Directory.Path == other.Directory.Path;
        }

        public override int GetHashCode() => HashCode.Combine(Node.Name, Directory.Path);

        public override bool Equals(object? obj) => obj is NodeDiskDir d && Equals(d);

        public override string ToString()
        {
            return $"{IPEndPoint.Parse(Node.Address).Address}:{Directory.Path}";
        }
    }
}