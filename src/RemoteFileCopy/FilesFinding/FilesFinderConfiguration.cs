namespace RemoteFileCopy.FilesFinding
{
    public class FilesFinderConfiguration
    {
        public FilesFinderConfiguration(HashType hashType)
        {
            HashType = hashType;
        }

        public HashType HashType { get; }
    }
}
