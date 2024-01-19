using System.Threading;
using System.Threading.Tasks;

namespace ClusterModifier;

public class ClusterExpander
{
    private readonly ClusterStateFinder _clusterStateFinder;
    private readonly WorkSpecificationFinder _workSpecificationFinder;
    private readonly ClusterStateAlterer _clusterStateAlterer;

    public ClusterExpander(
        ClusterStateFinder clusterStateFinder,
        WorkSpecificationFinder workSpecificationFinder,
        ClusterStateAlterer clusterStateAlterer
    )
    {
        _clusterStateFinder = clusterStateFinder;
        _workSpecificationFinder = workSpecificationFinder;
        _clusterStateAlterer = clusterStateAlterer;
    }

    public async Task ExpandCluster(CancellationToken cancellationToken)
    {
        var clusterState = await _clusterStateFinder.Find(cancellationToken);

        var workSpecification = _workSpecificationFinder.Find(clusterState);

        await _clusterStateAlterer.Alter(workSpecification, cancellationToken);
    }
}
