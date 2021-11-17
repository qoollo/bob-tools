using Newtonsoft.Json;

namespace BobApi.BobEntities
{
    public class NodeConfiguration
    {
        public NodeConfiguration(string rootDir)
        {
            RootDir = rootDir;
        }

        [JsonProperty("blob_file_name_prefix")]
        public string RootDir { get; }
    }
}