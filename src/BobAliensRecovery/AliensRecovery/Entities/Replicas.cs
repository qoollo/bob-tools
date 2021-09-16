using System.Collections.Generic;
using System.IO;
using System.Linq;
using BobApi.BobEntities;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class Replicas
    {
        private readonly IDictionary<string, RemoteDirectory> _remoteDirByNodeName;

        public Replicas(long vDiskId, IDictionary<string, RemoteDirectory> remoteDirByNodeName)
        {
            VDiskId = vDiskId;
            _remoteDirByNodeName = remoteDirByNodeName;
        }

        public long VDiskId { get; }

        public RemoteDirectory? FindRemoteDirectory(string nodeName)
        {
            if (_remoteDirByNodeName.TryGetValue(nodeName, out var remoteDirectory))
                return remoteDirectory;
            return null;
        }
    }
}