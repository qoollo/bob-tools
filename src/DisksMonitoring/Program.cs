using System;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobToolsCli;
using BobToolsCli.Exceptions;
using CommandLine;
using DisksMonitoring.Bob;
using DisksMonitoring.Config;
using DisksMonitoring.OS;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing.FileSystemAccessors;
using DisksMonitoring.OS.DisksFinding.LshwParsing;
using DisksMonitoring.OS.DisksProcessing;
using DisksMonitoring.OS.DisksProcessing.FSTabAltering;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace DisksMonitoring;

class Program
{
    static async Task Main(string[] args)
    {
        await CliHelper.RunWithParsed<MonitorArguments, GenerateOnlyArguments>(
            args,
            Monitor,
            GenerateConfiguration
        );
    }

    private static async Task Monitor(
        MonitorArguments args,
        IServiceCollection services,
        CancellationToken cancellationToken
    )
    {
        var prov = CreateServiceProvider(services);
        var generator = prov.GetRequiredService<ConfigurationGenerator>();
        var monitor = prov.GetRequiredService<Monitor>();
        var client = await args.GetLocalBobClient(cancellationToken);
        var config = await generator.Generate(args.StateFile, client, cancellationToken);
        await monitor.Run(config, cancellationToken);
    }

    private static async Task GenerateConfiguration(
        GenerateOnlyArguments args,
        IServiceCollection services,
        CancellationToken cancellationToken
    )
    {
        var prov = CreateServiceProvider(services);
        var generator = prov.GetRequiredService<ConfigurationGenerator>();
        var client = await args.GetLocalBobClient(cancellationToken);
        await generator.Generate(args.StateFile, client, cancellationToken);
    }

    static IServiceProvider CreateServiceProvider(IServiceCollection services)
    {
        services.AddTransient<LshwParser>();
        services.AddTransient<DisksFinder>();
        services.AddTransient<ProcessInvoker>();
        services.AddTransient<DisksFormatter>();
        services.AddTransient<DisksMounter>();
        services.AddTransient<NeededInfoStorage>();
        services.AddTransient<FSTabAlterer>();
        services.AddTransient<DisksMonitor>();
        services.AddTransient<ConfigGenerator>();
        services.AddTransient<BobPathPreparer>();
        services.AddSingleton<Configuration>();
        services.AddTransient<DisksStarter>();
        services.AddTransient<DisksCopier>();
        services.AddTransient<ExternalScriptsRunner>();
        services.AddTransient<DevPathDataFinder>();
        services.AddTransient<IFileSystemAccessor, LinuxFileSystemAccessor>();
        services.AddTransient<Monitor>().AddTransient<ConfigurationGenerator>();

        return services.BuildServiceProvider();
    }
}

[Verb("monitor", isDefault: true, HelpText = "Monitor disks unplugging")]
public class MonitorArguments : CommonWithSshArguments
{
    [Option("local-node", HelpText = "Local node name")]
    public string LocalNodeName { get; set; }

    [Option(
        "state-file",
        HelpText = "File in which state will be persisted",
        Default = "config.yaml"
    )]
    public string StateFile { get; set; }

    public async Task<BobApiClient> GetLocalBobClient(CancellationToken cancellationToken)
    {
        var confResult = await FindClusterConfiguration(cancellationToken: cancellationToken);
        if (confResult.IsOk(out var conf, out var err))
        {
            var node = conf.Nodes.Find(n => n.Name == LocalNodeName);
            if (node == null)
                throw new ConfigurationException(
                    $"Failed to find local node {LocalNodeName} in configuration"
                );
            return GetBobApiClientProvider().GetClient(node);
        }
        else
            throw new ConfigurationException("Failed to get cluster configuration");
    }
}

[Verb("generate-only", HelpText = "Perform only config generation")]
public class GenerateOnlyArguments : MonitorArguments { }
