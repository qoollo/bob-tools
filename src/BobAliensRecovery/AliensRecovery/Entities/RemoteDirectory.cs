using System.Net;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class RemoteDirectory
    {
        public RemoteDirectory(IPAddress iPAddress, string path)
        {
            IPAddress = iPAddress;
            Path = path;
        }

        public IPAddress IPAddress { get; }
        public string Path { get; }

        public override string ToString()
        {
            return $"{IPAddress}:{Path}";
        }
    }
}