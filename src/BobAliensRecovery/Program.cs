using System;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<ProgramArguments>(args);
            _ = await parsed.WithParsedAsync(RecoverAliens);
        }

        private static async Task RecoverAliens(ProgramArguments arguments)
        {
            var provider = CreateServiceProvider(arguments.LoggerOptions);
            var logger = provider.GetRequiredService<ILogger<Program>>();

            logger.LogDebug($"Received cluster config path: {arguments.ClusterConfigPath}");
            await Task.Delay(1);
        }

        private static IServiceProvider CreateServiceProvider(LoggerOptions loggerOptions)
        {
            var services = new ServiceCollection();

            services.AddLogging(b => b.AddConsole().SetMinimumLevel(loggerOptions.MinLevel));

            return services.BuildServiceProvider();
        }
    }
}
