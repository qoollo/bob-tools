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
            .AddTransient<WorkSpecificationFinder>()
            .AddTransient<ClusterStateAlterer>();
        if (arguments.TestRun)
        {
            services
                .AddTransient<
                    INodeDiskRemoteDirsFinder,
                    TestModeImplementations.CommonImplementation
                >()
                .AddTransient<IConfigurationsFinder, TestModeImplementations.CommonImplementation>()
                .AddTransient<ICopier, TestModeImplementations.CommonImplementation>()
                .AddTransient<IRemover, TestModeImplementations.CommonImplementation>();
        }
        else
        {
            services
                .AddTransient<INodeDiskRemoteDirsFinder, NodeDiskRemoteDirsFinder>()
                .AddTransient<IConfigurationsFinder, ConfigurationsFinder>()
                .AddTransient<ICopier, Copier>()
                .AddTransient<IRemover, Remover>();
        }
        services.AddRemoteFileCopy(arguments.SshConfiguration, arguments.FilesFinderConfiguration);
        using var provider = services.BuildServiceProvider();

        var expander = provider.GetRequiredService<ClusterExpander>();
        await expander.ExpandCluster(cancellationToken);
    }
}
