using System.Collections.Generic;
using System.Linq;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class BlobInfo
    {
        public BlobInfo(string blob, string? index, IEnumerable<string> files)
        {
            Blob = blob;
            Index = index;
            Files = files.ToArray();
        }

        public string Blob { get; }
        public string? Index { get; }
        public IEnumerable<string> Files { get; }

        public bool IsClosed => Index != null;

        public override string ToString()
        {
            return $"{Blob}" + (IsClosed ? ", closed" : "");
        }
    }
}
