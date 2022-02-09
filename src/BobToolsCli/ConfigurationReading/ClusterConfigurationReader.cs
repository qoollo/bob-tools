using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using YamlDotNet.Serialization;

namespace BobToolsCli.ConfigurationReading
{
    internal class ClusterConfigurationReader
    {

        public async Task<ConfigurationReadingResult<ClusterConfiguration>> ReadConfigurationFromFile(string fileName,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(fileName))
                return ConfigurationReadingResult<ClusterConfiguration>.Error($"Configuration file not found at {fileName}");

            var configContent = await File.ReadAllTextAsync(fileName, cancellationToken: cancellationToken);
            try
            {
                var config = new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
                    .Deserialize<ClusterConfiguration>(configContent);

                var nodeNames = config.Nodes.Select(n => n.Name).ToHashSet();
                var missingNodes = new HashSet<string>();
                var brokenVDiskIds = new HashSet<long>();
                foreach (var vd in config.VDisks)
                    foreach (var r in vd.Replicas)
                        if (!nodeNames.Contains(r.Node))
                        {
                            missingNodes.Add(r.Node);
                            brokenVDiskIds.Add(vd.Id);
                        }
                if (brokenVDiskIds.Count > 0)
                {
                    var ids = string.Join(", ", brokenVDiskIds);
                    var nodes = string.Join(", ", missingNodes);
                    var error = $"Configuration contains vdisks ({ids}) with not defined nodes ({nodes})";
                    return ConfigurationReadingResult<ClusterConfiguration>.Error(error);
                }

                return ConfigurationReadingResult<ClusterConfiguration>.Ok(config);
            }
            catch (Exception e)
            {
                return ConfigurationReadingResult<ClusterConfiguration>.Error($"Cluster configuration parsing error: {e.Message}");
            }
        }
    }
}