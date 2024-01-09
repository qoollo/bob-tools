using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClusterModifier;

public interface IRemover
{
    Task Remove(List<ConfirmedDeleteOperation> confirmed, List<UnconfirmedDeleteOperation> unconfirmed, bool forceRemoveUnconfirmed, CancellationToken cancellationToken);
}

