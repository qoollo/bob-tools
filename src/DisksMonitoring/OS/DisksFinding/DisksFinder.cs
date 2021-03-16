using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksFinding.LshwParsing;
using DisksMonitoring.OS.DisksProcessing;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.OS.DisksFinding
{
    class DisksFinder
    {
        private readonly LshwParser lshwParser;
        private readonly ProcessInvoker processInvoker;
        private readonly ILogger<DisksFinder> logger;

        public DisksFinder(LshwParser lshwParser, ProcessInvoker processInvoker, ILogger<DisksFinder> logger)
        {
            this.lshwParser = lshwParser;
            this.processInvoker = processInvoker;
            this.logger = logger;
        }

        public async Task<List<PhysicalDisk>> FindDisks()
        {
            var lshwOutput = await InvokeLshw();
            var parsedOutput = lshwParser.Parse(lshwOutput);
            var disks = parsedOutput.Where(n => n.Type == NodeType.Disk).ToList();
            var physicalDisks = disks.Select(ParseDisk).Where(d => d != null).ToList();
            logger.LogDisks(LogLevel.Debug, physicalDisks);
            return physicalDisks;
        }

        private PhysicalId CollectPhysicalId(LshwNode node)
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

        private PhysicalDisk ParseDisk(LshwNode diskNode)
        {
            var volumeNodes = diskNode.Children;
            var physicalIdStr = diskNode.FindSingleValue(TokenType.PhysicalId);
            var devPath = GetDevPath(diskNode);
            var upperPhysicalId = diskNode.Parent.FindSingleValue(TokenType.PhysicalId);
            var physicalId = CollectPhysicalId(diskNode);
            var volumes = volumeNodes.Select(v => ParseVolume(v, physicalId)).ToList();
            return new PhysicalDisk(physicalId, new DevPath(devPath), volumes);
        }

        private Volume ParseVolume(LshwNode volumeNode, PhysicalId diskPhysicalId)
        {
            var physicalIdStr = volumeNode.FindSingleValue(TokenType.PhysicalId);
            var devPath = GetDevPath(volumeNode);
            var uuid = volumeNode.FindSingleValue(TokenType.Serial);
            var state = volumeNode.FindSingleValue(TokenType.State);
            var mountPath = volumeNode.FindSingleValue(t => t.Type == TokenType.LogicalName && !t.Value.StartsWith("/dev"))
                ?? volumeNode.FindSingleValue(TokenType.LastMountPoint);
            var filesystem = volumeNode.FindSingleValue(TokenType.Filesystem);
            var physicalId = new PhysicalId(physicalIdStr, diskPhysicalId);
            var logicalVolumeNodes = volumeNode.Children;
            var logicalVolumes = logicalVolumeNodes.Select(lv => ParseLogicalVolume(lv, physicalId)).ToList();
            return new Volume(
                physicalId,
                new DevPath(devPath),
                new UUID(uuid),
                state is null ? State.NotMounted : Enum.Parse<State>(state, true),
                mountPath is null ? (MountPath?)null : new MountPath(mountPath),
                filesystem is null ? (Filesystem?)null : new Filesystem(filesystem),
                logicalVolumes);
        }

        private LogicalVolume ParseLogicalVolume(LshwNode logicalVolumeNode, PhysicalId volumePhysicalId)
        {
            var physicalId = logicalVolumeNode.FindSingleValue(TokenType.PhysicalId);
            var devPath = GetDevPath(logicalVolumeNode);
            var uuid = logicalVolumeNode.FindSingleValue(TokenType.Serial);
            return new LogicalVolume(
                new PhysicalId(physicalId, volumePhysicalId),
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
