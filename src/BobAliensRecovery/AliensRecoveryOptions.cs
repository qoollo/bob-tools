namespace BobAliensRecovery
{
    class AliensRecoveryOptions
    {
        public AliensRecoveryOptions(bool removeCopied,
            bool continueOnError)
        {
            RemoveCopied = removeCopied;
            ContinueOnError = continueOnError;
        }

        public bool RemoveCopied { get; }

        public bool ContinueOnError { get; }
    }
}