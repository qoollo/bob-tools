using System.Collections.Generic;

namespace BobApi.Entities
{
    public struct Directory
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public List<Directory> Children { get; set; }
    }
}
