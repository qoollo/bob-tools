using System.Collections.Generic;

namespace BobApi.Entities
{
    public class Directory
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public List<Directory> Children{ get; set; }
    }
}
