using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using Serilog;
using ProgramLogger = Microsoft.Extensions.Logging.ILogger<ClusterExpanding.Program>;
using CommandLine;
using BobApi.BobEntities;

namespace ClusterExpanding
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
            logger.LogInformation($"Expanding cluster from {options.OldConfigPath} to {options.NewConfigPath}");
            var oldConfig = await ClusterConfiguration.FromYamlFile(options.OldConfigPath);
            var newConfig = await ClusterConfiguration.FromYamlFile(options.NewConfigPath);
        }

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            AddSerilog(services);
            var result = services.BuildServiceProvider();
            logger = result.GetRequiredService<ProgramLogger>();
            return result;
        }

        static IConfiguration GetConfiguration()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false)
                .Build();
        }

        static void AddSerilog(IServiceCollection services)
        {
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(GetConfiguration())
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
        }
    }
}