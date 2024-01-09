using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.Exceptions;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Entities;

namespace ClusterModifier.TestModeImplementations;

public class CommonImplementation
    : IConfigurationsFinder,
        INodeDiskRemoteDirsFinder,
        ICopier,
        IRemover,
        IValidator,
        IBobDiskRestarter
{
    private readonly ClusterExpandArguments _args;
    private readonly ILogger<CommonImplementation> _logger;

    public CommonImplementation(ClusterExpandArguments args, ILogger<CommonImplementation> logger)
    {
        _args = args;
        _logger = logger;
    }

    public Task Copy(
        List<CopyOperation> copyOperations,
        int copyParallelDegree,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Performing {Count} copy operations, {Degree} max parallel",
            copyOperations.Count,
            copyParallelDegree
        );
        foreach (var op in copyOperations)
            _logger.LogInformation("Copy from {From} to {To}", op.From, op.To);
        return Task.CompletedTask;
    }

    public async Task<ClusterConfiguration> FindNewConfig(CancellationToken cancellationToken)
    {
        if (_args.BootstrapNode != null)
            throw new ConfigurationException("Only cluster configuration files can be specified");
        return (
            await _args.GetClusterConfigurationFromFile(_args.ClusterConfigPath, cancellationToken)
        ).Unwrap();
    }

    public async Task<ClusterConfiguration> FindOldConfig(CancellationToken cancellationToken)
    {
        return (
            await _args.GetClusterConfigurationFromFile(_args.OldConfigPath, cancellationToken)
        ).Unwrap();
    }

    public async Task<
        Dictionary<string, Dictionary<string, RemoteDir>>
    > FindRemoteAlienDirByDiskByNode(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, Dictionary<string, RemoteDir>>();
        foreach (var node in config.Nodes)
        {
            var addr = await node.FindIPAddress();
            result.Add(
                node.Name,
                node.Disks.ToDictionary(
                    d => d.Name,
                    d => new RemoteDir(addr, $"/{node.Name}/{d.Name}/alien")
                )
            );
        }
        return result;
    }

    public async Task<
        Dictionary<string, Dictionary<string, RemoteDir>>
    > FindRemoteRootDirByDiskByNode(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var result = new Dictionary<string, Dictionary<string, RemoteDir>>();
        foreach (var node in config.Nodes)
        {
            var addr = await node.FindIPAddress();
            result.Add(
                node.Name,
                node.Disks.ToDictionary(
                    d => d.Name,
                    d => new RemoteDir(addr, $"/{node.Name}/{d.Name}/bob")
                )
            );
        }
        return result;
    }

    public Task Remove(
        List<ConfirmedDeleteOperation> confirmed,
        List<UnconfirmedDeleteOperation> unconfirmed,
        bool forceRemoveUnconfirmed,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Performing {Count} confirmed remove operations", confirmed.Count);
        foreach (var op in confirmed)
            _logger.LogInformation(
                "Check that {@Dirs} contains all data from {Dir}, then remove source",
                op.Copies.Select(c => c.ToString()),
                op.DirToDelete
            );
        if (forceRemoveUnconfirmed)
            _logger.LogInformation("Will remove unconfirmed dirs even if error occured");
        if (unconfirmed.Count > 0)
            _logger.LogInformation(
                "Performing {Count} unconfirmed remove operations",
                unconfirmed.Count
            );
        foreach (var op in unconfirmed)
            _logger.LogInformation("Remove dir {Dir} without any confirmation", op.DirToDelete);
        return Task.CompletedTask;
    }

    public Task RestartDisk(NodeDisk disk, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Restarting disk {Disk} on node {Node}",
            disk.DiskName,
            disk.Node.Name
        );
        return Task.CompletedTask;
    }

    public Task Validate(ClusterState clusterState, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
