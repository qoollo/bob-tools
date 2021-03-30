using DisksMonitoring.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DisksMonitoring.OS.Helpers
{
    class ProcessInvoker
    {
        private readonly ILogger<ProcessInvoker> logger;

        public ProcessInvoker(ILogger<ProcessInvoker> logger)
        {
            this.logger = logger;
        }

        public Task<string[]> InvokeSudoProcess(string command, params string[] args)
        {
            return InvokeSudoProcessWithWD(command, null, args);
        }

        public Task<string[]> InvokeSudoProcessWithWD(string command, string workingDirectory, params string[] args)
        {
            var proc = GetSudoProcess(command, workingDirectory, args);
            logger.LogDebug($"Starting {command} with args {string.Join(", ", proc.StartInfo.ArgumentList.Skip(1))}");
            var tcs = CreateTCSForProcess(command, proc);
            proc.Start();
            return tcs.Task;
        }

        public async Task SetDirPermissionsAndOwner(string path, string permissions, string owner)
        {
            await InvokeSudoProcess("chmod", permissions, "-R", path);
            await InvokeSudoProcess("chown", owner, "-R", path);
        }

        private TaskCompletionSource<string[]> CreateTCSForProcess(string name, Process proc)
        {
            var tcs = new TaskCompletionSource<string[]>();
            var sw = Stopwatch.StartNew();
            proc.Exited += (sender, args) =>
            {
                if (proc.ExitCode != 0)
                {
                    logger.LogDebug($"{name} process exited with code {proc.ExitCode}, stderr: {Environment.NewLine}{proc.StandardError.ReadToEnd()}");
                    tcs.SetException(new ProcessFailedException(proc.StartInfo, proc.ExitCode));
                }
                else
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    logger.LogDebug($"Received output from {name}:{Environment.NewLine}{output}");
                    tcs.SetResult(output.Split(Environment.NewLine));
                    proc.Dispose();
                    sw.Stop();
                    logger.LogDebug($"{name} took {sw.Elapsed} to complete");
                }
            };
            return tcs;
        }

        private static Process GetSudoProcess(string command, string workingDirectory, params string[] args)
        {
            var res = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
                },
                EnableRaisingEvents = true
            };
            res.StartInfo.ArgumentList.Add(command);
            foreach (var arg in args)
                res.StartInfo.ArgumentList.Add(arg);
            return res;
        }
    }
}
