namespace BobAliensRecovery
{
    class AliensRecoveryOptions
    {
        public AliensRecoveryOptions(bool removeCopied)
        {
            RemoveCopied = removeCopied;
        }

        public bool RemoveCopied { get; }
    }
}