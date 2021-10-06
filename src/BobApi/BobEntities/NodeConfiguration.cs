using Newtonsoft.Json;

namespace BobApi.BobEntities
{
    public class NodeConfiguration
    {
        public NodeConfiguration(string rootDir)
        {
            RootDir = rootDir;
        }

        [JsonProperty("root_dir")]
        public string RootDir { get; }
    }
}