using DisksMonitoring.Config;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisksMonitoring.OS.DisksProcessing.FSTabAltering
{
    class FSTabAlterer
    {
        private const string fsTabFilename = "/etc/fstab";
        private const char commentStart = '#';

        private readonly ILogger<FSTabAlterer> logger;
        private readonly Configuration configuration;

        public FSTabAlterer(ILogger<FSTabAlterer> logger, Configuration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        public async Task UpdateFSTabRecord(IList<PhysicalDisk> disks)
        {
            var fsTabRecords = await ReadFSTab();
            var recordsToDelete = GetRecordsToDelete(disks, fsTabRecords);
            fsTabRecords.RemoveWhere(r => recordsToDelete.Contains(r));
            var newRecords = GetRecordsToAdd(disks, fsTabRecords);
            if (newRecords.Count > 0)
                await UpdateFSTab(fsTabRecords, newRecords);
        }

        private async Task UpdateFSTab(HashSet<FSTabRecord> oldRecords, HashSet<FSTabRecord> newRecords)
        {
            var newContent = new StringBuilder();
            var oldContent = await File.ReadAllLinesAsync(fsTabFilename);
            int added = newRecords.Count;
            int removed = 0;
            foreach (var line in oldContent)
                if (line.StartsWith(commentStart) || !FSTabRecord.TryParse(line, out var rec) || oldRecords.Contains(rec))
                    newContent.AppendLine(line);
                else
                    removed++;
            foreach (var rec in newRecords)
                newContent.AppendLine(rec.ToString());
            await File.WriteAllTextAsync(fsTabFilename, newContent.ToString());
            logger.LogInformation($"Changed {fsTabFilename}, {added} lines added, {removed} lines removed");
        }

        private HashSet<FSTabRecord> GetRecordsToDelete(IList<PhysicalDisk> disks, HashSet<FSTabRecord> fsTabRecords)
        {
            var recordsToDelete = new HashSet<FSTabRecord>();
            foreach (var disk in disks)
            {
                foreach (var volume in disk.Volumes)
                {
                    if (!volume.Mountable)
                        continue;
                    if (fsTabRecords.Any(r => r.Id.IsForVolume(volume)))
                        continue;

                    var existingRecord = fsTabRecords.FirstOrDefault(r => r.MountPath != null && r.MountPath.Equals(volume.MountPath));
                    if (existingRecord != null && !disks.Any(d => d.Volumes.Any(v => existingRecord.Id.IsForVolume(v))))
                    {
                        logger.LogInformation($"No disk found for {existingRecord.MountPath}, it should be deleted");
                        recordsToDelete.Add(existingRecord);
                    }

                }
            }

            return recordsToDelete;
        }

        private HashSet<FSTabRecord> GetRecordsToAdd(IList<PhysicalDisk> disks, HashSet<FSTabRecord> fsTabRecords)
        {
            var recordsToAdd = new HashSet<FSTabRecord>();
            foreach (var disk in disks)
            {
                foreach (var volume in disk.Volumes)
                {
                    if (fsTabRecords.Any(r => r.Id.IsForVolume(volume)))
                    {
                        logger.LogDebug($"{volume} is already in fstab");
                        continue;
                    }

                    if (volume.IsMounted && !fsTabRecords.Any(r => r.MountPath != null && r.MountPath.Equals(volume.MountPath)))
                    {
                        logger.LogInformation($"Record for {volume} not found, it should be created");
                        recordsToAdd.Add(new FSTabRecord(volume, configuration.FstabOptions));
                    }
                }
            }

            return recordsToAdd;
        }

        private async Task<HashSet<FSTabRecord>> ReadFSTab()
        {
            var lines = await File.ReadAllLinesAsync(fsTabFilename);
            var res = new HashSet<FSTabRecord>();
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                if (!line.StartsWith(commentStart) && FSTabRecord.TryParse(line, out var rec))
                    res.Add(rec);
            }
            foreach (var record in res)
                logger.LogDebug($"FSTab line: {record}");
            return res;
        }
    }
}
