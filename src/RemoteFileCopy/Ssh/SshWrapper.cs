using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

        public async Task InvokeSshProcess(IPAddress address,
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

            _logger.LogDebug("{stdout}", await process.StandardOutput.ReadToEndAsync());
            _logger.LogDebug("{stderr}", await process.StandardError.ReadToEndAsync());
        }
    }
}