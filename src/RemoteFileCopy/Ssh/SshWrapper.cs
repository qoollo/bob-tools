using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.DependenciesChecking;
using RemoteFileCopy.Exceptions;
using RemoteFileCopy.Ssh.Entities;

namespace RemoteFileCopy.Ssh
{
    public class SshWrapper
    {
        private readonly SshConfiguration _configuration;
        private readonly LocalDependenciesChecker _localDependenciesChecker;
        private readonly ILogger<SshWrapper> _logger;

        public SshWrapper(ILogger<SshWrapper> logger, SshConfiguration configuration,
            LocalDependenciesChecker localDependenciesChecker)
        {
            _configuration = configuration;
            _localDependenciesChecker = localDependenciesChecker;
            _logger = logger;
        }


        public IEnumerable<string> GetSshCommandAndArguments(bool withSpace)
        {
            var space = withSpace ? " " : "";
            var result = new List<string>
            {
                "-p" + space + _configuration.Port.ToString(),
                "-i" + space + _configuration.PathToKey,
                "-o" + space + "StrictHostKeyChecking=no",
            };
            foreach(var flag in _configuration.Flags)
                result.Add("-" + flag);
            return result;
        }

        public string SshCommand => _configuration.Cmd;
        public string SshUsername => _configuration.Username;

        public async Task<SshResult> InvokeSshProcess(IPAddress address,
            string command,
            CancellationToken cancellationToken = default)
        {
            if (!await _localDependenciesChecker.ProgramExists(SshCommand, cancellationToken))
                throw new MissingDependencyException(SshCommand);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = SshCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                }
            };
            foreach (var arg in GetSshCommandAndArguments(false))
                process.StartInfo.ArgumentList.Add(arg);

            process.StartInfo.ArgumentList.Add($"{_configuration.Username}@{address}");
            process.StartInfo.ArgumentList.Add(command);

            var (stdOutLines, stdErrLines) = await InvokeProcess(process, cancellationToken);

            return new SshResult(address, stdOutLines, stdErrLines);
        }

        private async Task<(string[] stdOut, string[] stdErr)> InvokeProcess(Process process, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Starting process {process.StartInfo.FileName} {string.Join(" ", process.StartInfo.ArgumentList)}");
            var stdOutLines = new List<string>();
            var stdErrLines = new List<string>();
            var stdOutHandler = CreateEventHandler(stdOutLines);
            var stdErrHandler = CreateEventHandler(stdErrLines);
            process.OutputDataReceived += stdOutHandler;
            process.ErrorDataReceived += stdErrHandler;
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                process.OutputDataReceived -= stdOutHandler;
                process.ErrorDataReceived -= stdErrHandler;
            }
            return (stdOutLines.ToArray(), stdErrLines.ToArray());
        }

        private static DataReceivedEventHandler CreateEventHandler(List<string> sink)
        {
            return (_, a) =>
            {
                if (a.Data != null)
                    lock (sink)
                        sink.Add(a.Data);
            };
        }
    }
}
