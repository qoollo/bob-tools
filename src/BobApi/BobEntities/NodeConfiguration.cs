using Newtonsoft.Json;

namespace BobApi.BobEntities
{
    public class NodeConfiguration
    {
        public NodeConfiguration(string rootDir)
        {
            RootDir = rootDir;
        }

        [JsonProperty("root_dir_name")]
        public string RootDir { get; }
    }
}