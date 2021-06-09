using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using Serilog;
using Serilog.Context;
using ProgramLogger = Microsoft.Extensions.Logging.ILogger<ClusterModifier.Program>;
using CommandLine;
using BobApi.BobEntities;
using System.Diagnostics;

namespace ClusterModifier
{
    public class Program
    {
        private static ProgramLogger logger;
        private static readonly IServiceProvider serviceProvider = CreateServiceProvider();

        public static async Task Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<ExpandClusterOptions>(args);
            var expandCluster = parsed.WithParsedAsync<ExpandClusterOptions>(ExpandCluster);
            await Task.WhenAll(expandCluster);
        }

        private static async Task ExpandCluster(ExpandClusterOptions options)
        {
            logger.LogDebug($"Expanding cluster from {options.OldConfigPath} to {options.NewConfigPath}");
            var oldConfig = await ClusterConfiguration.FromYamlFile(options.OldConfigPath);
            var newConfig = await ClusterConfiguration.FromYamlFile(options.NewConfigPath);
            foreach (var vdisk in newConfig.VDisks)
            {
                using var _ = logger.BeginScope("VDisk {vdiskId}", vdisk.Id);
                logger.LogDebug("Analyzing vdisk from new config");
                var oldVdisk = oldConfig.VDisks.Find(vd => vd.Id == vdisk.Id);
                if (oldVdisk != null && oldVdisk.Replicas.Count > 0)
                {
                    foreach (var replica in vdisk.Replicas)
                    {
                        using var __ = logger.BeginScope("Replica = {replicaNode}-{replicaDisk}", replica.Node, replica.Disk);
                        logger.LogDebug("Analyzing replica from new config");
                        var node = newConfig.Nodes.Find(n => n.Name == replica.Node);
                        var disk = node.Disks.Find(d => d.Name == replica.Disk);
                        using var ___ = logger.BeginScope("Path = {replicaPath}", disk.Path);
                        var oldReplica = oldVdisk.Replicas.Find(r => r.Node == replica.Node && r.Disk == replica.Disk);
                        if (oldReplica != null)
                            logger.LogDebug("Found replica in old config");
                        else
                        {
                            logger.LogWarning("Replica not found in old config, restoring data...");
                            foreach (var selectedReplica in oldVdisk.Replicas)
                            {
                                var oldNode = oldConfig.Nodes.Find(n => n.Name == selectedReplica.Node);
                                var oldDisk = oldNode.Disks.Find(d => d.Name == selectedReplica.Disk);
                                if (CopyReplica(vdisk, replica, selectedReplica, oldDisk.Path, disk.Path, options))
                                    break;
                                else
                                    logger.LogWarning($"Failed to copy from replica on node {selectedReplica.Node}");
                            }
                        }
                    }
                }
                else
                    logger.LogDebug($"Vdisk's replicas not found in old config");
            }
        }

        private static bool CopyReplica(
            ClusterConfiguration.VDisk vdisk,
            ClusterConfiguration.VDisk.Replica replica,
            ClusterConfiguration.VDisk.Replica oldReplica,
            string oldPath,
            string newPath,
            ExpandClusterOptions options)
        {
            var dsaPath = Path.GetFullPath(options.DiskStatusAnalyzer);
            var startInfo = new ProcessStartInfo
            {
                FileName = dsaPath,
                WorkingDirectory = Path.GetDirectoryName(dsaPath),
            };
            startInfo.ArgumentList.Add("copy-dir");
            startInfo.ArgumentList.Add($"-s {oldReplica.Node}");
            startInfo.ArgumentList.Add($"-d {replica.Node}");
            startInfo.ArgumentList.Add($"--source-dir \"{oldPath}{Path.PathSeparator}{vdisk.Id}\"");
            startInfo.ArgumentList.Add($"--dest-dir \"{newPath}{Path.PathSeparator}{vdisk.Id}\"");
            var process = new Process { StartInfo = startInfo };
            logger.LogInformation($"Starting process (pwd={startInfo.WorkingDirectory}) {startInfo.FileName} {string.Join(" ", process.StartInfo.ArgumentList)}");
            process.Start();
            process.WaitForExit();
            logger.LogInformation($"Process returned code {process.ExitCode}");
            return process.ExitCode == 0;
        }

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            AddSerilog(services);
            var result = services.BuildServiceProvider();
            logger = result.GetRequiredService<ProgramLogger>();
            return result;
        }

        static void AddSerilog(IServiceCollection services)
        {
            var template = "[{Timestamp:HH:mm:ss} {Level:u3}] {Scope}{NewLine}\t{Message:lj}{NewLine}{Exception}";
            var logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: template)
                .CreateLogger();
            services.AddLogging(b => b.AddSerilog(logger));
        }

        [Verb("expand", HelpText = "Expand cluster from old config to new config")]
        public class ExpandClusterOptions
        {
            [Option("old", Required = true, HelpText = "Path to old config")]
            public string OldConfigPath { get; set; }

            [Option("new", Required = true, HelpText = "Path to new config")]
            public string NewConfigPath { get; set; }

            [Option("dry-run", Required = false, HelpText = "Do not copy anything")]
            public bool DryRun { get; set; } = false;

            [Option("dsa", Required = true, HelpText = "Path to disk status analyzer")]
            public string DiskStatusAnalyzer { get; set; }
        }
    }
}