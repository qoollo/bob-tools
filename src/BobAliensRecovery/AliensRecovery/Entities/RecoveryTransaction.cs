namespace BobAliensRecovery.AliensRecovery.Entities
{
    class RecoveryTransaction
    {
        public RecoveryTransaction(RemoteDirectory from, RemoteDirectory to)
        {
            From = from;
            To = to;
        }

        public RemoteDirectory From { get; }
        public RemoteDirectory To { get; }

        public override string ToString()
        {
            return $"{From} to {To}";
        }
    }
}