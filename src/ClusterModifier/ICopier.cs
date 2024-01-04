using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClusterModifier;

public interface ICopier
{
    Task Copy(List<CopyOperation> copyOperations, int copyParallelDegree, CancellationToken cancellationToken);
}

