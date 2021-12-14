using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BobToolsCli
{
    public static class CliHelper
    {
        private static readonly Parser s_parser = Parser.Default;

        public static async Task RunWithParsed<TArgs>(string[] args,
            Func<TArgs, IServiceCollection, CancellationToken, Task> proc)
            where TArgs : CommonArguments
            => await WithToken(async t => await s_parser
                .ParseArguments<TArgs>(args)
                .MapResult(WithBasicServices<TArgs, Task>((a, s) => proc(a, s, t)), ProcessErrors));

        public static async Task RunWithParsed<TArgs1, TArgs2>(string[] args,
            Func<TArgs1, IServiceCollection, CancellationToken, Task> proc1,
            Func<TArgs2, IServiceCollection, CancellationToken, Task> proc2)
            where TArgs1 : CommonArguments
            where TArgs2 : CommonArguments
            => await WithToken(async t => await s_parser
                .ParseArguments<TArgs1, TArgs2>(args)
                .MapResult(
                    WithBasicServices<TArgs1, Task>((a, s) => proc1(a, s, t)),
                    WithBasicServices<TArgs2, Task>((a, s) => proc2(a, s, t)),
                    ProcessErrors));

        private static async Task WithToken(Func<CancellationToken, Task> f)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            try
            {
                await f(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Cancelled");
            }
        }

        private static Func<T, R> WithBasicServices<T, R>(Func<T, IServiceCollection, R> f)
            where T : CommonArguments
        {
            return args =>
            {
                var services = new ServiceCollection()
                    .AddLogging(b => b.AddConsole().SetMinimumLevel(args.GetMinLogLevel()))
                    .AddSingleton(args)
                    .AddSingleton(args.GetNodePortStorage());

                return f(args, services);
            };
        }

        private static Task ProcessErrors(IEnumerable<Error> errs)
        {
            // This should be enabled only for debug purposes
            // Console.WriteLine($"Errors: {string.Join(", ", errs)}");
            return Task.CompletedTask;
        }
    }
}