using System.Threading;
using System.Threading.Tasks;

namespace ClusterModifier;

public class ClusterStateAlterer
{
    private readonly DataManipulator _dataManipulator;
    private readonly BobServersManipulator _bobServersManipulator;

    public ClusterStateAlterer(DataManipulator dataManipulator,
            BobServersManipulator bobServersManipulator)
    {
        _dataManipulator = dataManipulator;
        _bobServersManipulator = bobServersManipulator;
    }

    public async Task Alter(
        WorkSpecification workSpecification,
        CancellationToken cancellationToken
    )
    {
        await _dataManipulator.Manipulate(workSpecification, cancellationToken);
        await _bobServersManipulator.Manipulate(workSpecification, cancellationToken);
    }
}
