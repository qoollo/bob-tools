using System;
using System.Linq;
using System.Threading.Tasks;
using BobApi;
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
        var nodeObj = await api.GetStatus();
        if (nodeObj is null)
            throw new Exception("Failed to get node status");
        var vdisks = nodeObj.Value.VDisks;
        var nodes = await api.GetNodes();
        if (nodes is null)
            throw new Exception("Failed to get nodes information");

        var apiByName = nodes.ToDictionary(n => n.Name, n =>
        {
            var url = new Uri($"{baseNode.Scheme}://{n.Address}");
            var addr = $"{baseNode.Scheme}://{url.Host}:{baseNode.Port}";
            logger.LogInformation(addr);
            return new BobApiClient(new Uri(addr));
        });

        var unique = 0L;
        var withReplicas = 0L;
        foreach (var vdisk in vdisks)
        {
            var maxCount = 0L;
            var totalCount = 0L;
            foreach (var replica in vdisk.Replicas)
            {
                if (!apiByName.ContainsKey(replica.Node))
                    logger.LogWarning($"Node {replica.Node} mentioned in replicas of vdisk {vdisk.Id} not found");
                else
                {
                    var countObj = await apiByName[replica.Node].CountRecordsOnVDisk(vdisk);
                    if (countObj is null)
                        logger.LogWarning($"Failed to get count of records from {replica.Node} for vdisk {vdisk.Id}");
                    else
                    {
                        var count = countObj.Value;
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
