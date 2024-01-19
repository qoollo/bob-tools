using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli;
using BobToolsCli.Helpers;
using OldPartitionsRemover.Entities;
using OldPartitionsRemover.Infrastructure;

namespace OldPartitionsRemover;

public class RemovablePartitionsFinder
{
    private readonly ResultsCombiner _resultsCombiner;
    private readonly BobApiClientProvider _bobApiProvider;

    public RemovablePartitionsFinder(ResultsCombiner resultsCombiner, CommonArguments args)
    {
        _resultsCombiner = resultsCombiner;
        _bobApiProvider = args.GetBobApiClientProvider();
    }

    public async Task<Result<List<RemovablePartition>>> Find(
        ClusterConfiguration config,
        bool allowAliens,
        CancellationToken cancellationToken
    )
    {
        var vDisksByNode = GetVDisksByNode(config);
        return await _resultsCombiner.CollectResults(
            config.Nodes,
            async node =>
                await FindOnNode(config, node, allowAliens, vDisksByNode, cancellationToken)
        );
    }

    public async Task<Result<List<RemovablePartition>>> FindOnNode(
        ClusterConfiguration config,
        ClusterConfiguration.Node node,
        bool allowAliens,
        CancellationToken cancellationToken
    )
    {
        var vDisksByNode = GetVDisksByNode(config);
        return await FindOnNode(config, node, allowAliens, vDisksByNode, cancellationToken);
    }

    private async Task<Result<List<RemovablePartition>>> FindOnNode(
        ClusterConfiguration config,
        ClusterConfiguration.Node node,
        bool allowAliens,
        Dictionary<string, List<long>> vDisksByNode,
        CancellationToken cancellationToken
    )
    {
        if (allowAliens)
            return await _resultsCombiner.CollectResults(
                FindNormalOnNode(config, node, cancellationToken),
                FindAlienOnNode(vDisksByNode, node, cancellationToken)
            );
        else
            return await FindNormalOnNode(config, node, cancellationToken);
    }

    private Dictionary<string, List<long>> GetVDisksByNode(ClusterConfiguration config)
    {
        var result = config.Nodes.ToDictionary(n => n.Name, _ => new List<long>());
        foreach (var vDisk in config.VDisks)
        foreach (var replica in vDisk.Replicas)
        {
            if (result.TryGetValue(replica.Node, out var vDisks))
                vDisks.Add(vDisk.Id);
        }
        return result;
    }

    private async Task<Result<List<RemovablePartition>>> FindNormalOnNode(
        ClusterConfiguration config,
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        // API is not disposed because it will be captured in removable partitions
        var api = _bobApiProvider.GetClient(node);
        var vDisksByDisk = GetVDisksByDisk(config, node);
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
        Dictionary<string, List<long>> vDisksByNode,
        ClusterConfiguration.Node node,
        CancellationToken cancellationToken
    )
    {
        // API is not disposed because it will be captured in removable partitions
        var api = _bobApiProvider.GetClient(node);
        return await _resultsCombiner.CollectResults(
            vDisksByNode.Where(kv => kv.Key != node.Name),
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
        BobApiClient api,
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
        BobApiClient api,
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
}
