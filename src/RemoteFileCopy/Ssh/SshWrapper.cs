using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
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


        public IEnumerable<string> GetSshCommandAndArguments()
        {
            return new[]
            {
                "-p" + _configuration.Port.ToString(),
                "-i" + _configuration.PathToKey,
                "-oStrictHostKeyChecking=no",
            };
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
            foreach (var arg in GetSshCommandAndArguments())
                process.StartInfo.ArgumentList.Add(arg);

            process.StartInfo.ArgumentList.Add($"{_configuration.Username}@{address}");
            process.StartInfo.ArgumentList.Add(command);

            _logger.LogDebug($"Starting process {process.StartInfo.FileName} {string.Join(" ", process.StartInfo.ArgumentList)}");

            process.Start();

            await process.WaitForExitAsync(cancellationToken);

            var stdOut = (await process.StandardOutput.ReadToEndAsync())
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var stdErr = (await process.StandardError.ReadToEndAsync())
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new SshResult(address, stdOut, stdErr);
        }
    }
}