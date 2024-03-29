using Newtonsoft.Json;

namespace BobApi
{
    public struct Partition
    {
        [JsonProperty("vdisk_id")]
        public long VDiskId { get; set; }

        [JsonProperty("node_name")]
        public string NodeName { get; set; }

        [JsonProperty("disk_name")]
        public string DiskName { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("records_count")]
        public ulong RecordsCount { get; set; }
    }
}
