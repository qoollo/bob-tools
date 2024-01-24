using Newtonsoft.Json;

namespace BobApi.Entities
{
    public class PartitionSlim
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }
    }
}
