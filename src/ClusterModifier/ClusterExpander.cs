using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ClusterModifier;

public class ClusterExpander
{
    private readonly ClusterStateFinder _clusterStateFinder;
    private readonly WorkSpecificationFinder _workSpecificationFinder;
    private readonly ClusterStateAlterer _clusterStateAlterer;
    private readonly ICopier _copier;
    private readonly IRemover _remover;
    private readonly ClusterExpandArguments _args;
    private readonly ILogger<ClusterExpander> _logger;

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
