using BobApi;
using DisksMonitoring.Config;
using DisksMonitoring.Entities;
using DisksMonitoring.OS;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.LshwParsing;
using DisksMonitoring.OS.DisksProcessing;
using DisksMonitoring.OS.DisksProcessing.FSTabAltering;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace DisksMonitoring
{
    class Program
    {
        const string configFile = "config.yaml";
        static IServiceProvider serviceProvider;
        static ILogger<Program> logger;

        static void Initialize(LogLevel logLevel)
        {
            var services = new ServiceCollection();
            services.AddLogging(c => c.AddConsole().SetMinimumLevel(logLevel));
            services.AddTransient<LshwParser>();
            services.AddTransient<DisksFinder>();
            services.AddTransient<ProcessInvoker>();
            services.AddTransient<DisksFormatter>();
            services.AddTransient<DisksMounter>();
            services.AddTransient<NeededInfoStorage>();
            services.AddTransient<FSTabAlterer>();
            services.AddTransient<DisksMonitor>();
            services.AddTransient<ConfigGenerator>();
            services.AddTransient<BobPathPreparer>();
            services.AddSingleton<Configuration>();

            serviceProvider = services.BuildServiceProvider();
            logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        }

        static async Task Main(string[] args)
        {
            const string help == "--help";
            const string levelArgPrefix = "--level";
            const string genOnlyOption = "--gen-only";

            if (args.Contains(help))
            {
                Console.WriteLine($"Available options: --level=LOGLEVEL, --gen-only");
                return;
            }

            LogLevel level = LogLevel.Information;
            var levelArg = args.FirstOrDefault(arg => arg.StartsWith(levelArgPrefix));
            if (levelArg != null && levelArg.Length > levelArgPrefix.Length + 1 && Enum.TryParse<LogLevel>(levelArg.Substring(levelArgPrefix.Length + 1), true, out var argLevel))
                level = argLevel;
            Initialize(level);

            var monitor = serviceProvider.GetRequiredService<DisksMonitor>();
            var bobApiClient = new BobApiClient(new Uri("http://127.0.0.1:8000"));
            var configuration = await GetConfiguration(bobApiClient);
            if (args.Contains(genOnlyOption))
                return;

            var span = TimeSpan.FromSeconds(configuration.MinCycleTimeSec);
            var lastInfo = new HashSet<BobDisk>();
            logger.LogInformation("Start monitor");
            while (true)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var deadInfo = await GetDeadInfo(configuration, lastInfo);
                    await monitor.CheckAndUpdate();
                    await configuration.SaveToFile(configFile);
                    await StartDisks(monitor, bobApiClient, configuration, deadInfo);
                }
                catch (Exception e)
                {
                    logger.LogError($"Exception while processing cycle: {e.Message}{Environment.NewLine}{e.StackTrace}");
                }
                sw.Stop();
                if (sw.Elapsed < span)
                    await Task.Delay(span - sw.Elapsed);
            }
        }

        private static async Task StartDisks(DisksMonitor monitor, BobApiClient bobApiClient, Configuration configuration, List<BobDisk> deadInfo)
        {
            var newDead = await configuration.GetDeadInfo();
            foreach (var i in deadInfo.Except(newDead))
            {
                configuration.SaveUUID(await monitor.GetUUID(i));
                logger.LogInformation($"Starting bobdisk {i}...");
                int retry = 0;
                while (!await bobApiClient.StartDisk(i.DiskNameInBob) && retry++ < configuration.StartRetryCount)
                    logger.LogWarning($"Failed to start bobdisk in try {retry}, trying again");
                if (retry == configuration.StartRetryCount)
                    logger.LogError($"Failed to start bobdisk {i}");
                else
                    logger.LogInformation($"Bobdisk {i} started");
            }
        }

        private static async Task<List<BobDisk>> GetDeadInfo(Configuration configuration, HashSet<BobDisk> lastInfo)
        {
            var deadInfo = await configuration.GetDeadInfo();
            if (!lastInfo.SetEquals(deadInfo))
            {
                foreach (var i in deadInfo)
                    logger.LogWarning($"Missing bobdisk: {i}");
                lastInfo.Clear();
                lastInfo.UnionWith(deadInfo);
            }

            return deadInfo;
        }

        private static async Task<Configuration> GetConfiguration(BobApiClient bobApiClient)
        {
            logger.LogInformation("Reading configuration...");
            var configuration = serviceProvider.GetRequiredService<Configuration>();
            try
            {
                await configuration.AddEntriesFromBob(bobApiClient);
                await configuration.SaveKnownReadyUuids();
                await configuration.ReadFromFile(configFile);
                await configuration.SaveToFile(configFile);
                return configuration;
            }
            catch (Exception e)
            {
                logger.LogError($"Exception while getting info from config: {e.Message}{Environment.NewLine}{e.StackTrace}");
                throw;
            }
            finally
            {
                logger.LogInformation("Reading configuration done");
            }
        }
    }
}
