namespace RemoteFileCopy.Entities
{
    public struct CopyResult
    {
        public CopyResult(bool isError, string[] files)
        {
            IsError = isError;
            Files = files;
        }

        public bool IsError { get; }
        public string[] Files { get; }
    }
}
