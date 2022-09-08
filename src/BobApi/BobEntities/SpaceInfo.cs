using Newtonsoft.Json;

namespace BobApi.BobEntities
{
    public class SpaceInfo
    {
        [JsonProperty("total_disk_space_bytes")]
        public ulong TotalDiskSpaceBytes { get; set; }

        [JsonProperty("free_disk_space_bytes")]
        public ulong FreeDiskSpaceBytes { get; set; }

        [JsonProperty("used_disk_space_bytes")]
        public ulong UsedDiskSpaceBytes { get; set; }

        [JsonProperty("occupied_disk_space_bytes")]
        public ulong OccupiedDiskSpaceBytes { get; set; }
    }
}