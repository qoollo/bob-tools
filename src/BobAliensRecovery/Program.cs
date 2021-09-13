using System;
using System.Threading.Tasks;
using CommandLine;

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
            Console.WriteLine($"Received cluster config path: {arguments.ClusterConfigPath}");
        }
    }
}
