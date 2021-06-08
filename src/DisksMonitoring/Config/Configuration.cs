using BobApi;
using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksProcessing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DisksMonitoring.Config
{
    class Configuration
    {
        private static readonly IDeserializer deserializer = new DeserializerBuilder().WithTypeConverter(new YamlConverter())
            .WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        private static readonly ISerializer serializer = new SerializerBuilder().WithTypeConverter(new YamlConverter())
            .WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

        private readonly ConfigGenerator configGenerator;
        private readonly DisksFinder disksFinder;
        private readonly ILogger<Configuration> logger;

        public Configuration() { }
        public Configuration(ConfigGenerator configGenerator, DisksFinder disksFinder, ILogger<Configuration> logger)
        {
            this.configGenerator = configGenerator;
            this.disksFinder = disksFinder;
            this.logger = logger;
        }

        public string MountPointPermissions { get; set; } = "777";
        public string MountPointOwner { get; set; } = "root:root";
        public string BobDirPermissions { get; set; } = "777";
        public string BobDirOwner { get; set; } = "root:root";
        public string FstabOptions { get; set; } = "nofail";
        public HashSet<BobDisk> MonitoringEntries { get; set; } = new HashSet<BobDisk>();
        public HashSet<UUID> KnownUuids { get; set; } = new HashSet<UUID>();
        public int StartRetryCount { get; set; } = 10;
        public int MinCycleTimeSec { get; set; } = 5;
        public string Filesystem { get; set; } = "ext4";
        public string PathToDiskStatusAnalyzer { get; set; } = null;
        public int MaxUmountRetries { get; set; } = 3;
        public List<string> PreCycleScripts { get; set; } = new List<string>();
        public List<string> PostCycleScripts { get; set; } = new List<string>();

        public async Task ReadFromFile(string filename)
        {
            if (!File.Exists(filename))
                return;

            var content = await File.ReadAllTextAsync(filename);
            var parsed = deserializer.Deserialize<Configuration>(content);
            foreach (var prop in GetType().GetProperties().Where(p => p.SetMethod != null && p.GetMethod != null))
            {
                var value = prop.GetValue(parsed);
                if (value != null)
                    prop.SetValue(this, value);
            }
        }

        public async Task SaveToFile(string filename)
        {
            var serialized = serializer.Serialize(this);
            await File.WriteAllTextAsync(filename, serialized);
        }

        public async Task AddEntriesFromBob(BobApiClient bobApiClient)
        {
            var disks = await disksFinder.FindDisks();
            var infos = await configGenerator.GenerateConfigFromBob(bobApiClient);
            foreach (var i in infos)
            {
                logger.LogInformation($"Received {i} from bob");
                var conflict = MonitoringEntries.FirstOrDefault(bd => bd.DiskNameInBob == i.DiskNameInBob && bd.BobPath.Equals(i.BobPath) && !bd.Equals(i));
                if (conflict != null)
                {
                    logger.LogWarning($"Conflict: disk from bob {i} conflicts with disk from config {conflict}");
                    continue;
                }
                var uuid = disks.SelectMany(d => d.Volumes).FirstOrDefault(v => v.PhysicalId.Equals(i.PhysicalId) && v.IsFormatted)?.UUID;
                if (uuid != null)
                    SaveUUID(uuid);
                MonitoringEntries.Add(i);
            }
        }

        public async Task SaveKnownReadyUuids()
        {
            var disks = await disksFinder.FindDisks();
            foreach (var i in MonitoringEntries)
            {
                var uuid = disks.SelectMany(d => d.Volumes).FirstOrDefault(v => v.PhysicalId.Equals(i.PhysicalId) && v.IsFormatted)?.UUID;
            }
        }

        public void SaveUUID(UUID uuid)
        {
            if (!KnownUuids.Contains(uuid))
            {
                logger.LogInformation($"Volume {uuid} saved");
                KnownUuids.Add(uuid);
            }
        }
    }
}
