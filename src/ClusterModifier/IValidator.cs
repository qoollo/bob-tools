using System.Threading;
using System.Threading.Tasks;

namespace ClusterModifier;

public interface IValidator
{
    Task Validate(ClusterState clusterState, CancellationToken cancellationToken);
}
