using System.Collections.Generic;

namespace BobApi.Entities
{
    public class Directory
    {
        public Directory(string name, string path, List<Directory> children)
        {
            Name = name;
            Path = path;
            Children = children;
        }

        public string Name { get; }
        public string Path { get; }
        public List<Directory> Children { get; }
    }
}
