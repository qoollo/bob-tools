namespace BobAliensRecovery
{
    class AliensRecoveryOptions
    {
        public AliensRecoveryOptions(bool removeSource)
        {
            RemoveSource = removeSource;
        }

        public bool RemoveSource { get; }
    }
}