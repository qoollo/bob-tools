using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BobApi;
using CommandLine;
using DisksMonitoring.Bob;
using DisksMonitoring.Config;
using DisksMonitoring.OS;
using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing.FileSystemAccessors;
using DisksMonitoring.OS.DisksFinding.LshwParsing;
using DisksMonitoring.OS.DisksProcessing;
using DisksMonitoring.OS.DisksProcessing.FSTabAltering;
using DisksMonitoring.OS.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            services.AddLogging(c => c.AddConsole(ops => ops.TimestampFormat = "[hh:mm:ss] ").SetMinimumLevel(logLevel));
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
            services.AddTransient<ExternalScriptsRunner>();
            services.AddTransient<DevPathDataFinder>();
            services.AddTransient<IFileSystemAccessor, LinuxFileSystemAccessor>();

            serviceProvider = services.BuildServiceProvider();
            logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        }

        static async Task Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<MonitorOptions, GenerateOnlyOptions>(args);
            await Task.WhenAll(
                parsed.WithParsedAsync<MonitorOptions>(Monitor),
                parsed.WithParsedAsync<GenerateOnlyOptions>(GenerateConfiguration));
        }

        private static async Task Monitor(MonitorOptions options)
        {
            var configuration = await GenerateConfiguration(options);
            var monitor = serviceProvider.GetRequiredService<DisksMonitor>();
            var externalScriptsRunner = serviceProvider.GetRequiredService<ExternalScriptsRunner>();
            var disksStarter = serviceProvider.GetRequiredService<DisksStarter>();
            var span = TimeSpan.FromSeconds(configuration.MinCycleTimeSec);
            var client = GetBobApiClient(options);
            logger.LogInformation("Start monitor");
            while (true)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await externalScriptsRunner.RunPreCycleScripts();
                    await monitor.CheckAndUpdate(client);
                    await configuration.SaveToFile(configFile);
                    await disksStarter.StartDisks(client);
                    await externalScriptsRunner.RunPostCycleScripts();
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

        private static async Task<Configuration> GenerateConfiguration(MonitorOptions options)
        {
            Initialize(options.LogLevel);
            return await GetConfiguration(GetBobApiClient(options));
        }

        private static BobApiClient GetBobApiClient(MonitorOptions options)
        {
            return new BobApiClient(new Uri($"http://127.0.0.1:{options.Port}"), options.Username, options.Password);
        }

        private static async Task<Configuration> GetConfiguration(BobApiClient bobApiClient)
        {
            logger.LogInformation("Reading configuration...");
            var configuration = serviceProvider.GetRequiredService<Configuration>();
            try
            {
                await configuration.ReadFromFile(configFile);
                await configuration.AddEntriesFromBob(bobApiClient);
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

            [Option("port", Required = false, HelpText = "Local bob http api port", Default = 8000)]
            public int Port { get; set; }

            [Option("user", HelpText = "Local bob username")]
            public string Username { get; set; }

            [Option("password", HelpText = "Local bob password")]
            public string Password { get; set; }
        }

        [Verb("generate-only", HelpText = "Perform only config generation")]
        public class GenerateOnlyOptions : MonitorOptions
        {
        }
    }
}
