using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public interface IRemover
{
    Task Remove(List<ConfirmedDeleteOperation> confirmed, List<RemoteDir> unconfirmed, bool forceRemoveUnconfirmed, CancellationToken cancellationToken);
}

