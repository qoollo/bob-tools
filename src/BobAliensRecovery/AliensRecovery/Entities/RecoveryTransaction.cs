using RemoteFileCopy.Entities;
using static BobApi.BobEntities.ClusterConfiguration;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class RecoveryTransaction
    {
        public RecoveryTransaction(RemoteDir from, RemoteDir to, string targetNodeName, Replicas source)
        {
            From = from;
            To = to;
            TargetNodeName = targetNodeName;
            Source = source;
        }

        public RemoteDir From { get; }
        public RemoteDir To { get; }
        public string TargetNodeName { get; }
        public Replicas Source { get; }

        public override string ToString()
        {
            return $"{From} to {To}";
        }
    }
}