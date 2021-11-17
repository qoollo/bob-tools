using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BobApi;
using BobApi.Entities;
using Microsoft.Extensions.Logging;

public class ClusterRecordsCounter
{
    private readonly ILogger<ClusterRecordsCounter> logger;

    public ClusterRecordsCounter(ILogger<ClusterRecordsCounter> logger)
    {
        this.logger = logger;
    }

    public async Task<(long unique, long withReplicas)> CountRecordsInCluster(Uri baseNode)
    {
        using var api = new BobApiClient(baseNode);
        var nodeObjResult = await api.GetStatus();
        if (!nodeObjResult.TryGetData(out var nodeObj))
            throw new Exception("Failed to get node status");
        var vdisks = nodeObj.VDisks;
        var nodesResult = await api.GetNodes();
        if (!nodesResult.TryGetData(out var nodes))
            throw new Exception("Failed to get nodes information");

        var apiByName = nodes.ToDictionary(n => n.Name, n =>
        {
            var url = new Uri($"{baseNode.Scheme}://{n.Address}");
            var addr = $"{baseNode.Scheme}://{url.Host}:{baseNode.Port}";
            logger.LogInformation($"Found cluster node {addr}");
            return new BobApiClient(new Uri(addr));
        });

        return await CountRecords(apiByName, vdisks);
    }

    public async Task<(long max, long total)> CountRecords(Dictionary<string, BobApiClient> apiByName, IEnumerable<VDisk> vdisks)
    {
        var unique = 0L;
        var withReplicas = 0L;
        var nodesToRemove = new List<string>();
        foreach (var (name, api) in apiByName)
        {
            var status = await api.GetStatus();
            if (status.TryGetError(out var _))
            {
                logger.LogInformation($"Removing unavailable node {name}");
                nodesToRemove.Add(name);
            }
        }
        foreach (var name in nodesToRemove)
            apiByName.Remove(name);
        foreach (var vdisk in vdisks)
        {
            var maxCount = 0L;
            var totalCount = 0L;
            foreach (var replica in vdisk.Replicas)
            {
                if (!apiByName.ContainsKey(replica.Node) && !nodesToRemove.Contains(replica.Node))
                    logger.LogWarning($"Node {replica.Node} mentioned in replicas of vdisk {vdisk.Id} not found");
                else if (apiByName.ContainsKey(replica.Node))
                {
                    var countObjResult = await apiByName[replica.Node].CountRecordsOnVDisk(vdisk);
                    if (!countObjResult.TryGetData(out var countObj))
                        logger.LogWarning($"Failed to get count of records from {replica.Node} for vdisk {vdisk.Id}");
                    else
                    {
                        var count = countObj;
                        if (count > maxCount)
                            maxCount = count;
                        totalCount += count;
                    }
                }
            }
            unique += maxCount;
            withReplicas += totalCount;
        }

        return (unique, withReplicas);
    }
}
