using System.Collections.Generic;
using System.IO;
using System.Linq;
using BobApi.BobEntities;
using RemoteFileCopy.Entities;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class Replicas
    {
        private readonly IDictionary<string, RemoteDir> _remoteDirByNodeName;

        public Replicas(long vDiskId, IDictionary<string, RemoteDir> remoteDirByNodeName)
        {
            VDiskId = vDiskId;
            _remoteDirByNodeName = remoteDirByNodeName;
        }

        public long VDiskId { get; }

        public RemoteDir? FindRemoteDirectory(string nodeName)
        {
            if (_remoteDirByNodeName.TryGetValue(nodeName, out var remoteDirectory))
                return remoteDirectory;
            return null;
        }
    }
}