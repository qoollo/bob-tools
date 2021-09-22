using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Ssh.Entities;

namespace RemoteFileCopy.Ssh
{
    public class SshWrapper
    {
        private readonly SshConfiguration _configuration;
        private readonly ILogger<SshWrapper> _logger;

        public SshWrapper(ILogger<SshWrapper> logger, SshConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }


        public IEnumerable<IEnumerable<string>> GetSshCommandAndArguments()
        {
            return new[]
            {
                new[] { "-p", _configuration.Port.ToString() },
                new[] { "-i", _configuration.PathToKey },
                new[] { "-o", "StrictHostKeyChecking=no" },
            };
        }

        public string SshCommand => _configuration.Cmd;
        public string SshUsername => _configuration.Username;

        public async Task<SshResult> InvokeSshProcess(IPAddress address,
            string command,
            CancellationToken cancellationToken = default)
        {
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
            {
                var argument = new StringBuilder(arg.First());
                foreach (var value in arg.Skip(1))
                    argument.Append($"{value}");
                process.StartInfo.ArgumentList.Add(argument.ToString());
            }
            process.StartInfo.ArgumentList.Add($"{_configuration.Username}@{address}");
            process.StartInfo.ArgumentList.Add(command);

            _logger.LogDebug($"Starting process {process.StartInfo.FileName} {string.Join(" ", process.StartInfo.ArgumentList)}");

            process.Start();

            await process.WaitForExitAsync(cancellationToken);

            var stdOut = (await process.StandardOutput.ReadToEndAsync()).Split(Environment.NewLine);
            var stdErr = (await process.StandardError.ReadToEndAsync()).Split(Environment.NewLine);

            return new SshResult(stdOut, stdErr);
        }
    }
}