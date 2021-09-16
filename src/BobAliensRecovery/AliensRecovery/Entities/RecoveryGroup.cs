using System.Collections.Generic;

namespace BobAliensRecovery.AliensRecovery.Entities
{
    class RecoveryGroup
    {
        public RecoveryGroup(long vDiskId, IDictionary<string, string> diskByNodeName)
        {
            VDiskId = vDiskId;
            DiskByNodeName = diskByNodeName;
        }

        public long VDiskId { get; }
        public IDictionary<string, string> DiskByNodeName { get; }
    }
}