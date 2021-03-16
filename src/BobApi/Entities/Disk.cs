using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BobApi.Entities
{
    public struct Disk
    {
        public string Path { get; set; }
        public string Name { get; set; }
        [JsonProperty("is_active")]
        public bool IsActive { get; set; }
    }
}
