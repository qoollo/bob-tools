using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.Exceptions;
using OldPartitionsRemover.Entities;
using OldPartitionsRemover.Infrastructure;

namespace OldPartitionsRemover;

public class RemovablePartitionsFinder
{
    private readonly ResultsCombiner _resultsCombiner;
    private readonly IBobApiClientFactory _bobApiClientFactory;
    private readonly RemoverArguments _removerArguments;

    public RemovablePartitionsFinder(
        ResultsCombiner resultsCombiner,
        IBobApiClientFactory bobApiClientFactory,
        RemoverArguments removerArguments
    )
    {
        _resultsCombiner = resultsCombiner;
        _bobApiClientFactory = bobApiClientFactory;
        _removerArguments = removerArguments;
    }

    public async Task<Result<List<RemovablePartition>>> Find(
        ClusterConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var vDisksConfiguration = GetVDisksConfiguration(config);
        return await _resultsCombiner.CollectResults(
            config.Nodes,
            async node => await FindOnNode(vDisksConfiguration, node, cancellationToken)
        );
    }

    public async Task<Result<List<RemovablePartition>>> FindOnNode(
        ClusterConfiguration config,
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        var vDisksConfiguration = GetVDisksConfiguration(config);
        return await FindOnNode(vDisksConfiguration, node, cancellationToken);
    }

    private async Task<Result<List<RemovablePartition>>> FindOnNode(
        VDisksConfiguration vDisksConfiguration,
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        if (_removerArguments.AllowAlien)
            return await _resultsCombiner.CollectResults(
                FindNormalOnNode(vDisksConfiguration, node, cancellationToken),
                FindAlienOnNode(vDisksConfiguration, node, cancellationToken)
            );
        else
            return await FindNormalOnNode(vDisksConfiguration, node, cancellationToken);
    }

    private VDisksConfiguration GetVDisksConfiguration(ClusterConfiguration config)
    {
        var vDisksByNode = config.Nodes.ToDictionary(n => n.Name, _ => new HashSet<long>());
        var vDisksByDiskByNode = config.Nodes.ToDictionary(
            n => n.Name,
            _ => new Dictionary<string, List<long>>()
        );
        foreach (var vDisk in config.VDisks)
        foreach (var replica in vDisk.Replicas)
        {
            if (vDisksByNode.TryGetValue(replica.Node, out var vDisksHs))
                vDisksHs.Add(vDisk.Id);
            if (vDisksByDiskByNode.TryGetValue(replica.Node, out var vDisksByDisk))
            {
                if (vDisksByDisk.TryGetValue(replica.Disk, out var vDisks))
                    vDisks.Add(vDisk.Id);
                else
                    vDisksByDisk.Add(replica.Disk, new List<long> { vDisk.Id });
            }
        }
        return new VDisksConfiguration(vDisksByDiskByNode, vDisksByNode);
    }

    private async Task<Result<List<RemovablePartition>>> FindNormalOnNode(
        VDisksConfiguration vDisksConfiguration,
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        // API is not disposed because it will be captured in removable partitions
        var api = _bobApiClientFactory.GetPartitionsBobApiClient(node);
        if (!vDisksConfiguration.VDisksByDiskByNode.TryGetValue(node.Name, out var vDisksByDisk))
            throw new ConfigurationException($"Node {node} is not presented in replicas");
        return await _resultsCombiner.CollectResults(
            vDisksByDisk,
            async kv =>
                await _resultsCombiner.CollectResults(
                    kv.Value,
                    async vDisk => await FindNormal(api, kv.Key, vDisk, cancellationToken)
                )
        );
    }

    private async Task<Result<List<RemovablePartition>>> FindAlienOnNode(
        VDisksConfiguration vDisksConfiguration,
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        // API is not disposed because it will be captured in removable partitions
        var api = _bobApiClientFactory.GetPartitionsBobApiClient(node);
        return await _resultsCombiner.CollectResults(
            vDisksConfiguration.VDisksByNode.Where(kv => kv.Key != node.Name),
            async kv =>
                await _resultsCombiner.CollectResults(
                    kv.Value,
                    async vDisk => await FindAlien(api, kv.Key, vDisk, cancellationToken)
                )
        );
    }

    private Dictionary<string, List<long>> GetVDisksByDisk(
        ClusterConfiguration config,
        ClusterConfiguration.Node node
    )
    {
        var result = node.Disks.ToDictionary(d => d.Name, _ => new List<long>());
        foreach (var vDisk in config.VDisks)
        foreach (var replica in vDisk.Replicas)
        {
            if (replica.Node == node.Name && result.TryGetValue(replica.Disk, out var vDisks))
                vDisks.Add(vDisk.Id);
        }
        return result;
    }

    private async Task<Result<List<RemovablePartition>>> FindNormal(
        IPartitionsBobApiClient api,
        string disk,
        long vDisk,
        CancellationToken cancellationToken
    )
    {
        Result<List<PartitionSlim>> apiResult = await api.GetPartitionSlims(
            disk,
            vDisk,
            cancellationToken
        );
        return apiResult.Map(
            ps =>
                ps.Select(
                        p =>
                            CreateRemovablePartition(
                                p,
                                async ct => await api.DeletePartitionById(disk, vDisk, p.Id, ct)
                            )
                    )
                    .ToList()
        );
    }

    private async Task<Result<List<RemovablePartition>>> FindAlien(
        IPartitionsBobApiClient api,
        string node,
        long vDisk,
        CancellationToken cancellationToken
    )
    {
        Result<List<PartitionSlim>> apiResult = await api.GetAlienPartitionSlims(
            node,
            vDisk,
            cancellationToken
        );
        return apiResult.Map(
            ps =>
                ps.Select(
                        p =>
                            CreateRemovablePartition(
                                p,
                                async ct =>
                                    await api.DeleteAlienPartitionById(node, vDisk, p.Id, ct)
                            )
                    )
                    .ToList()
        );
    }

    private RemovablePartition CreateRemovablePartition(
        PartitionSlim p,
        RemoveRemovablePartition remove
    )
    {
        return new RemovablePartition(
            p.Id,
            DateTimeOffset.FromUnixTimeSeconds(p.Timestamp),
            remove
        );
    }

    private record struct VDisksConfiguration(
        Dictionary<string, Dictionary<string, List<long>>> VDisksByDiskByNode,
        Dictionary<string, HashSet<long>> VDisksByNode
    );
}
