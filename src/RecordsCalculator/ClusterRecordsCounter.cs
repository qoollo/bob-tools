using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli.Helpers;
using Microsoft.Extensions.Logging;
using RecordsCalculator.Entities;

namespace RecordsCalculator
{
    public class ClusterRecordsCounter
    {
        private readonly ILogger<ClusterRecordsCounter> _logger;
        private readonly ProgramArguments _programArguments;
        private readonly NodePortStorage _nodePortStorage;

        public ClusterRecordsCounter(ILogger<ClusterRecordsCounter> logger,
            ProgramArguments programArguments)
        {
            _logger = logger;
            _programArguments = programArguments;
            _nodePortStorage = programArguments.GetNodePortStorage();
        }

        public async Task<RecordsCount> CountRecordsInCluster(ClusterConfiguration clusterConfiguration, CancellationToken cancellationToken = default)
        {
            var nodeByName = clusterConfiguration.Nodes
                .ToDictionary(n => n.Name, n => new BobApiClient(_nodePortStorage.GetNodeApiUri(n)));

            long unique = 0, withReplicas = 0;
            foreach (var vDisk in clusterConfiguration.VDisks)
            {
                _logger.LogInformation("Calculating values on vdisk {vdiskId}, {replicasCount} replicas", vDisk.Id, vDisk.Replicas.Count);

                var counts = await GetCounts(nodeByName, vDisk, cancellationToken);

                var (maxCount, totalCount) = counts
                    .Select(r => r.TryGetData(out var d) ? d : 0)
                    .Aggregate((max: 0L, total: 0L), (t, n) => (n > t.max ? n : t.max, t.total + n));

                _logger.LogInformation("Found {unique} total, {withReplicas} with replicas on vdisk {vdiskId}", maxCount, totalCount, vDisk.Id);

                unique += maxCount;
                withReplicas += totalCount;
            }
            return new RecordsCount(unique, withReplicas);
        }

        private async Task<BobApiResult<long>[]> GetCounts(Dictionary<string, BobApiClient> nodeByName, ClusterConfiguration.VDisk vDisk, CancellationToken cancellationToken)
        {
            var results = await Task.WhenAll(vDisk.Replicas.Select(r => nodeByName[r.Node].CountRecordsOnVDisk(vDisk.Id, cancellationToken)));

            if (results.Any(r => r.IsError))
            {
                var nodesWithErrors = vDisk.Replicas.Zip(results).Where(t => t.Second.IsError)
                    .Select(t => $"{t.First.Node}: {(t.Second.TryGetError(out var e) ? e : 0)}");
                var errors = string.Join(", ", nodesWithErrors);

                _logger.LogError("Error counting values for vdisk {vdiskId}: {errors}", vDisk.Id, string.Join(", ", errors));
                if (!_programArguments.ContinueOnError)
                    throw new ProcessInterruptException();
            }

            return results;
        }
    }
}