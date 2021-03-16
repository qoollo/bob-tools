using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.Helpers
{
    static class ILoggerExtensions
    {
        public static void LogDisks<T>(this ILogger<T> logger, LogLevel logLevel, IEnumerable<PhysicalDisk> disks, string prefix = "")
        {
            var message = new StringBuilder(Environment.NewLine);
            foreach (var disk in disks)
            {
                message.AppendLine(disk.ToString());
                foreach (var volume in disk.Volumes)
                {
                    message.AppendLine("\t" + volume.ToString());
                    foreach (var logicalVolume in volume.LogicalVolumes)
                        message.AppendLine("\t\t" + logicalVolume.ToString());
                }
            }
            logger.Log(logLevel, prefix + message.ToString());
        }
    }
}
