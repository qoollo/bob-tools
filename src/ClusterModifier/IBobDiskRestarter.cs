using System.Threading;
using System.Threading.Tasks;

namespace ClusterModifier;

public interface IBobDiskRestarter
{
    Task RestartDisk(NodeDisk disk, CancellationToken cancellationToken);
}
