using RemoteFileCopy.Entities;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class RecoveryTransaction
    {
        public RecoveryTransaction(RemoteDir from, RemoteDir to)
        {
            From = from;
            To = to;
        }

        public RemoteDir From { get; }
        public RemoteDir To { get; }

        public override string ToString()
        {
            return $"{From} to {To}";
        }
    }
}