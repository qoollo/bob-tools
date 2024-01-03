using System.Threading;
using System.Threading.Tasks;
using BobToolsCli;
using Microsoft.Extensions.DependencyInjection;
using RemoteFileCopy.Extensions;

namespace ClusterModifier;

public class Program
{
    public static async Task Main(string[] args)
    {
        await CliHelper.RunWithParsed<ClusterExpandArguments>(args, ExpandCluster);
    }

    private static async Task ExpandCluster(
        ClusterExpandArguments arguments,
        IServiceCollection services,
        CancellationToken cancellationToken
    )
    {
        services
            .AddTransient<ClusterExpander>()
            .AddTransient<ClusterStateFinder>()
            .AddTransient<NodeDiskRemoteDirsFinder>()
            .AddTransient<WorkSpecificationFinder>()
            .AddTransient<Copier>()
            .AddTransient<Remover>();
        services.AddRemoteFileCopy(arguments.SshConfiguration, arguments.FilesFinderConfiguration);
        using var provider = services.BuildServiceProvider();

        var expander = provider.GetRequiredService<ClusterExpander>();
        await expander.ExpandCluster(cancellationToken);
    }
}
