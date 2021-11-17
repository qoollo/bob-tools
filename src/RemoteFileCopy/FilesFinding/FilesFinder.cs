using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.DependenciesChecking;
using RemoteFileCopy.Entities;
using RemoteFileCopy.Exceptions;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy.FilesFinding
{
    public class FilesFinder
    {
        private static readonly Regex s_fileInfo = new(@"f(.+) l(\d+) c(.+)");

        private readonly SshWrapper _sshWrapper;
        private readonly ILogger<FilesFinder> _logger;
        private readonly RemoteDependenciesChecker _remoteDependenciesChecker;
        private readonly string _scriptContent;

        public FilesFinder(SshWrapper sshWrapper, ILogger<FilesFinder> logger,
            RemoteDependenciesChecker remoteDependenciesChecker)
        {
            _sshWrapper = sshWrapper;
            _logger = logger;
            _remoteDependenciesChecker = remoteDependenciesChecker;

            var scriptName = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Single(s => s.EndsWith("Scripts.list_files.sh"));
            using var scriptStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(scriptName);
            if (scriptStream is null)
                throw new ArgumentException("Failed to find script for files listing");

            using var reader = new StreamReader(scriptStream);
            _scriptContent = reader.ReadToEnd();
        }

        public async Task<IEnumerable<RemoteFileInfo>> FindFiles(RemoteDir dir,
            CancellationToken cancellationToken = default)
        {
            if (!await _remoteDependenciesChecker.RemoteProgramExists(dir.Address, "xxhsum", cancellationToken))
                throw new MissingDependencyException("xxhsum");

            var scriptFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(scriptFile, _scriptContent, cancellationToken: cancellationToken);
            try
            {
                var sshResult = await _sshWrapper.InvokeSshProcess(dir.Address, $"sh -s < {scriptFile} {dir.Path}", cancellationToken);

                if (sshResult.IsError)
                {
                    _logger.LogWarning("Failed to get files from {dir}", dir);
                    _logger.LogDebug("Finder stderr: {output}", string.Join(Environment.NewLine, sshResult.StdErr));
                    _logger.LogDebug("Finder stdout: {output}", string.Join(Environment.NewLine, sshResult.StdOut));
                    throw new CommandLineFailureException("find");
                }

                var result = new List<RemoteFileInfo>();
                foreach (var line in sshResult.StdOut)
                {
                    var match = s_fileInfo.Match(line);
                    if (match.Success && long.TryParse(match.Groups[2].Value, out var size))
                        result.Add(new RemoteFileInfo(dir.Address, match.Groups[1].Value, size, match.Groups[3].Value));
                    else
                        _logger.LogDebug("Failed to parse {line} into file info", line);
                }
                return result;
            }
            finally
            {
                File.Delete(scriptFile);
            }
        }
    }
}