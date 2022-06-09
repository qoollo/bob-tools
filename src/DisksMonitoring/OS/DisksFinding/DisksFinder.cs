using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksFinding.LshwParsing;
using DisksMonitoring.OS.DisksProcessing;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;

namespace DisksMonitoring.OS.DisksFinding
{
    class DisksFinder
    {
        private readonly LshwParser lshwParser;
        private readonly ProcessInvoker processInvoker;
        private readonly ILogger<DisksFinder> logger;
        private readonly DevPathDataFinder devPathDataFinder;

        public DisksFinder(LshwParser lshwParser, ProcessInvoker processInvoker, ILogger<DisksFinder> logger,
            DevPathDataFinder devPathDataFinder)
        {
            this.lshwParser = lshwParser;
            this.processInvoker = processInvoker;
            this.logger = logger;
            this.devPathDataFinder = devPathDataFinder;
        }

        public async Task<List<PhysicalDisk>> FindDisks()
        {
            var lshwOutput = await InvokeLshw();
            var parsedOutput = lshwParser.Parse(lshwOutput);
            var disks = parsedOutput.Where(n => n.Type == NodeType.Disk).ToList();
            var physicalIdByDevPath = devPathDataFinder.FindPhysicalIdByDevPath();
            var physicalDisks = disks.Select(d => ParseDisk(d, physicalIdByDevPath)).Where(d => d != null).ToList();
            logger.LogDisks(LogLevel.Debug, physicalDisks);
            return physicalDisks;
        }

        private static PhysicalId CollectPhysicalId(LshwNode node)
        {
            var physIds = new Stack<string>();
            string physId;
            while (node != null && (physId = node.FindSingleValue(TokenType.PhysicalId)) != null)
            {
                physIds.Push(physId);
                node = node.Parent;
            }
            PhysicalId res = null;
            while (physIds.TryPop(out var s))
                res = new PhysicalId(s, res);
            return res;
        }

        private PhysicalDisk ParseDisk(LshwNode diskNode, Dictionary<DevPath, PhysicalId> physicalIdByDevPath)
        {
            var product = diskNode.FindSingleValue(TokenType.Product);
            var devPath = GetDevPath(diskNode);
            if (product is null || devPath is null) // not a real disk
                return null;
            var volumeNodes = diskNode.Children;
            var physicalId = CollectPhysicalId(diskNode);
            if (physicalIdByDevPath.TryGetValue(new DevPath(devPath), out var id))
                physicalId = id;
            var volumes = volumeNodes.Select(v => ParseVolume(v, physicalId, physicalIdByDevPath)).Where(v => v != null).ToList();
            return new PhysicalDisk(physicalId, new DevPath(devPath), volumes);
        }

        private static Volume ParseVolume(LshwNode volumeNode, PhysicalId diskPhysicalId,
            Dictionary<DevPath, PhysicalId> physicalIdByDevPath)
        {
            var physicalIdStr = volumeNode.FindSingleValue(TokenType.PhysicalId);
            var devPath = GetDevPath(volumeNode);
            if (devPath == null)
                return null;
            var uuid = volumeNode.FindSingleValue(TokenType.Serial);
            var state = volumeNode.FindSingleValue(TokenType.State);
            var mountPath = volumeNode.FindSingleValue(t => t.Type == TokenType.LogicalName && !t.Value.StartsWith("/dev"))
                ?? volumeNode.FindSingleValue(TokenType.LastMountPoint);
            var filesystem = volumeNode.FindSingleValue(TokenType.Filesystem);
            var physicalId = new PhysicalId(physicalIdStr, diskPhysicalId);
            if (physicalIdByDevPath.TryGetValue(new DevPath(devPath), out var id))
                physicalId = id;
            var logicalVolumeNodes = volumeNode.Children;
            var logicalVolumes = logicalVolumeNodes.Select(lv => ParseLogicalVolume(lv, physicalId, physicalIdByDevPath)).ToList();
            var mountOptions = volumeNode.FindSingleValue(TokenType.MountOptions);
            return new Volume(physicalId,
                              new DevPath(devPath),
                              new UUID(uuid),
                              state is null ? State.NotMounted : Enum.Parse<State>(state, true),
                              mountPath is null ? null : new MountPath(mountPath),
                              filesystem is null ? null : new Filesystem(filesystem),
                              logicalVolumes,
                              mountOptions is null ? null : new MountOptions(mountOptions));
        }

        private static LogicalVolume ParseLogicalVolume(LshwNode logicalVolumeNode, PhysicalId volumePhysicalId,
            Dictionary<DevPath, PhysicalId> physicalIdByDevPath)
        {
            var devPath = GetDevPath(logicalVolumeNode);
            var physicalId = new PhysicalId(logicalVolumeNode.FindSingleValue(TokenType.PhysicalId), volumePhysicalId);
            if (physicalIdByDevPath.TryGetValue(new DevPath(devPath), out var id))
                physicalId = id;
            var uuid = logicalVolumeNode.FindSingleValue(TokenType.Serial);
            return new LogicalVolume(
                physicalId,
                new DevPath(devPath),
                new UUID(uuid));
        }

        private static string GetDevPath(LshwNode node)
        {
            return node.FindSingleValue(t => t.Type == TokenType.LogicalName && t.Value.StartsWith("/dev"));
        }

        private async Task<IList<string>> InvokeLshw()
        {
            return await processInvoker.InvokeSudoProcess("lshw", "-c", "storage", "-c", "disk", "-c", "volume");
        }
    }
}
