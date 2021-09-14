using System.Net;

namespace RemoteFileCopy.Entites
{
    public class RemoteFile
    {
        internal RemoteFile(IPEndPoint endPoint, Path path)
        {
            EndPoint = endPoint;
            Path = path;
        }

        public IPEndPoint EndPoint { get; }
        public Path Path { get; }

        public override string ToString()
        {
            return $"{EndPoint}:{Path}";
        }
    }
}