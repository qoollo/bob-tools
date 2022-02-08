using System;
using System.Collections.Generic;
using System.Net;

namespace BobApi.Entities
{
    public struct Node
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public List<VDisk> VDisks { get; set; }

        public Uri GetUri() => new Uri($"http://{Address}");
    }
}
