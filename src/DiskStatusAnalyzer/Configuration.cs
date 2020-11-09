using System.Collections.Generic;

namespace DiskStatusAnalyzer
{
    public class Configuration
    {
        public List<NodeInfo> NodeInfos { get; set; }
        public string RsyncCmd { get; set; } = "rsync";
        public string SshCmd { get; set; } = "ssh";
        public string PathToSshKey { get; set; }
        public string SshUsername { get; set; }
        public bool CopyAliens { get; set; }
        public bool RestartAfterCopy { get; set; }
        public bool RemoveCopiedFiles { get; set; }

        public class NodeInfo
        {
            public string Host { get; set; }
            public int SshPort { get; set; }
            public string InnerNetworkHost { get; set; }

            public override string ToString() => Host;
        }
    }
}
