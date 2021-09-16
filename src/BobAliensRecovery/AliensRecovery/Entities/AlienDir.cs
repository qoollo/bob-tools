using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BobApi.BobEntities;
using BobApi.Entities;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class AlienDir : IEquatable<AlienDir>
    {
        public AlienDir(ClusterConfiguration.Node node, Directory directory)
        {
            Node = node;
            Directory = directory;
            Children = Directory.Children.Select(c => new AlienDir(node, c)).ToArray();
        }

        public ClusterConfiguration.Node Node { get; }
        public Directory Directory { get; }
        public IEnumerable<AlienDir> Children { get; }

        public string DirName => Directory.Name;

        public IEnumerable<AlienDir> GetMatchedDirs(Func<AlienDir, bool> predicate)
        {
            if (predicate(this))
                yield return this;
            foreach (var child in Children)
                foreach (var match in child.GetMatchedDirs(predicate))
                    yield return match;
        }

        public bool Equals(AlienDir? other)
        {
            if (other is null)
                return false;

            return Node.Name == other.Node.Name
                && Directory.Path == other.Directory.Path;
        }

        public override int GetHashCode() => HashCode.Combine(Node.Name, Directory.Path);

        public override bool Equals(object? obj) => obj is AlienDir d && Equals(d);

        public override string ToString()
        {
            return $"{IPEndPoint.Parse(Node.Address).Address}:{Directory.Path}";
        }
    }
}