using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
using BobToolsCli.Exceptions;
using BobToolsCli.Helpers;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Exceptions;
using Serilog.Extensions.Logging.File;

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
            var handler = GetCancelKeyPressHandler(cancellationTokenSource);
            Console.CancelKeyPress += handler;

            try
            {
                await f(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Cancelled");
            }
            catch (ClusterStateException e)
            {
                Console.WriteLine($"Cluster state is invalid: {e.Message}");
            }
            catch (ConfigurationException e)
            {
                Console.WriteLine($"Configuration is invalid: {e.Message}");
            }
            catch (OperationException e)
            {
                Console.WriteLine($"Execution failed: {e.Message}");
            }

            catch (MissingDependencyException e)
            {
                Console.WriteLine($"Missing dependency: {e.Message}");
            }
            catch (CommandLineFailureException e)
            {
                Console.WriteLine($"Command line failure: {e.Message}");
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }

        private static Func<T, R> WithBasicServices<T, R>(Func<T, IServiceCollection, R> f)
            where T : CommonArguments
        {
            return args =>
            {
                var services = new ServiceCollection()
                    .AddLogging(b =>
                    {
                        if (!string.IsNullOrWhiteSpace(args.FileLogPath))
                            b.AddFile(args.FileLogPath, minimumLevel: args.GetMinLogLevel());
                        b.AddConsole().SetMinimumLevel(args.GetMinLogLevel());
                    })
                    .AddSingleton(args)
                    .AddSingleton<CommonArguments>(args)
                    .AddSingleton<IConfigurationFinder>(args)
                    .AddSingleton(args.GetBobApiClientProvider())
                    .AddSingleton<IBobApiClientFactory, PortBasedBobApiClientFactory>()
                    .AddTransient<ParallelP2PProcessor>();

                return f(args, services);
            };
        }

        private static Task ProcessErrors(IEnumerable<Error> errs)
        {
            // This should be enabled only for debug purposes
            // Console.WriteLine($"Errors: {string.Join(", ", errs)}");
            return Task.CompletedTask;
        }


        private static ConsoleCancelEventHandler GetCancelKeyPressHandler(CancellationTokenSource cancellationTokenSource)
        {
            return (s, e) =>
            {
                cancellationTokenSource.Cancel();
            };
        }
    }
}
