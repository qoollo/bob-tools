using BobApi;
using CommandLine;
using DisksMonitoring.Bob;
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
            services.AddTransient<DisksStarter>();
            services.AddTransient<DisksCopier>();

            serviceProvider = services.BuildServiceProvider();
            logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        }

        static async Task Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<MonitorOptions, GenerateOnlyOptions>(args);
            await Task.WhenAll(
                parsed.WithParsedAsync<MonitorOptions>(Monitor),
                parsed.WithParsedAsync<GenerateOnlyOptions>(ops => GenerateConfiguration(ops.LogLevel)));
        }

        private static async Task Monitor(MonitorOptions options)
        {
            var configuration = await GenerateConfiguration(options.LogLevel);
            var monitor = serviceProvider.GetRequiredService<DisksMonitor>();
            var disksStarter = serviceProvider.GetRequiredService<DisksStarter>();
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
                    await disksStarter.StartDisks(GetBobApiClient(), deadInfo);
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

        private static async Task<Configuration> GenerateConfiguration(LogLevel logLevel)
        {
            Initialize(logLevel);
            return await GetConfiguration(GetBobApiClient());
        }

        private static BobApiClient GetBobApiClient()
        {
            return new BobApiClient(new Uri("http://127.0.0.1:8000"));
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

        [Verb("monitor", isDefault: true, HelpText = "Monitor disks unplugging")]
        public class MonitorOptions
        {
            [Option("log-level", Required = false, HelpText = "Logging level", Default = LogLevel.Information)]
            public LogLevel LogLevel { get; set; }
        }

        [Verb("generate-only", HelpText = "Perform only config generation")]
        public class GenerateOnlyOptions
        {
            [Option("log-level", Required = false, HelpText = "Logging level", Default = LogLevel.Information)]
            public LogLevel LogLevel { get; set; }
        }
    }
}
