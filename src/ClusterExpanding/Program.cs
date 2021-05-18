using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using Serilog;
using ProgramLogger = Microsoft.Extensions.Logging.ILogger<ClusterExpanding.Program>;

namespace ClusterExpanding
{
    public class Program
    {
        private static ProgramLogger logger;
        private static readonly IServiceProvider serviceProvider = CreateServiceProvider();

        public static async Task Main(string[] args)
        {
            logger.LogInformation("Hello world!");
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
    }
}