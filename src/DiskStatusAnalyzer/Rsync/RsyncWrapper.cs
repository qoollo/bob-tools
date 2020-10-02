using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiskStatusAnalyzer.Rsync.Entities;
using Microsoft.Extensions.Logging;

namespace DiskStatusAnalyzer.Rsync
{
    public class RsyncWrapper
    {
        private const string syncedFilesFilename = "synced_files";
        private static readonly HashSet<string> ExcludedFiles = new HashSet<string> { "*synced_files", "*lock" };
        private static readonly HashSet<string> ForbiddenNames = new HashSet<string> { "." };
        private readonly ILogger<RsyncWrapper> logger;

        public RsyncWrapper(ILogger<RsyncWrapper> logger)
        {
            this.logger = logger;
        }

        public async Task<bool> Copy(RsyncEntry frm, RsyncEntry to)
        {
            var excludedFilesString = string.Join(' ', ExcludedFiles.Select(f => $"--exclude {f}"));
            var copyCommand = $"rsync -e 'ssh -o \"StrictHostKeyChecking=no\"' {excludedFilesString} " +
                              $"-av {frm.Path}/ {to.Configuration.SshUsername}@{to.Configuration.InnerUri.Host}:{to.Path}";
            var copyResult = await InvokeSshCommand(frm.Configuration, copyCommand);
            if (!copyResult)
                return false;
            return await SaveSyncedFiles(frm, to);
        }

        private async Task<bool> SaveSyncedFiles(RsyncEntry frm, RsyncEntry to)
        {
            var fromResult = await FindFilesWithSha(frm.Configuration, frm.Path);
            var toResult = await FindFilesWithSha(to.Configuration, to.Path);
            foreach (var line in fromResult)
                if (!toResult.Contains(line))
                {
                    logger.LogDebug($"File \"{line}\" not synced!");
                }

            fromResult.RemoveAll(line => !toResult.Contains(line));

            fromResult.RemoveAll(line => ExcludedFiles.Any(f => line.Contains(f.Trim('*'))));

            var syncedFilesFullFilename = $"{frm.Path}/{syncedFilesFilename}";
            var echoCommand = $"echo -e \"{string.Join("\n", fromResult)}\"" +
                              $" > {syncedFilesFullFilename}";

            return await InvokeSshCommand(frm.Configuration, echoCommand);
        }

        public Task<List<string>> FindFilesWithSha(RsyncEntry entry) =>
            FindFilesWithSha(entry.Configuration, entry.Path);

        public Task<List<string>> FindFilesWithSha(ConnectionInfo connectionConfiguration,
                                                   string path)
        {
            var command = $"find {path} -type f -exec sha256sum {{}} \\;";
            return InvokeSshCommandWithOutput(connectionConfiguration, command);
        }

        public Task<List<string>> FindSyncedFiles(RsyncEntry entry) =>
            FindSyncedFiles(entry.Configuration, entry.Path);

        public Task<List<string>> FindSyncedFiles(ConnectionInfo connectionInfo, string path)
        {
            var command = $"cat {path}/{syncedFilesFilename}";
            return InvokeSshCommandWithOutput(connectionInfo, command);
        }

        public Task<bool> RemoveFiles(RsyncEntry entry, IEnumerable<string> filenames) =>
            RemoveFiles(entry.Configuration, filenames);

        public Task<bool> RemoveFiles(ConnectionInfo connectionInfo, IEnumerable<string> filenames)
        {
            var command = $"rm -f {string.Join(' ', filenames)}";
            return InvokeSshCommand(connectionInfo, command);
        }

        public Task<bool> InvokeSshCommand(ConnectionInfo configuration, string command)
        {
            var process = configuration.GetSshProcess(command, false);
            LogProcessStart(process);
            var tcs = new TaskCompletionSource<bool>();
            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                    logger.LogDebug(
                        $"Process {ProcessToString(process)} failed, stderr:{Environment.NewLine}{process.StandardError.ReadToEnd()}");
                tcs.SetResult(process.ExitCode == 0);
                process.Dispose();
            };
            process.Start();
            return tcs.Task;
        }

        public Task<List<string>> InvokeSshCommandWithOutput(ConnectionInfo configuration,
                                                             string command)
        {
            var process = configuration.GetSshProcess(command, true);
            LogProcessStart(process);
            var tcs = new TaskCompletionSource<List<string>>();
            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                    logger.LogDebug(
                        $"Process {ProcessToString(process)} failed, stderr:{Environment.NewLine}{process.StandardError.ReadToEnd()}");
                var result = new List<string>();
                string s;
                while ((s = process.StandardOutput.ReadLine()) != null)
                    result.Add(s);
                tcs.SetResult(result);
                process.Dispose();
            };
            process.Start();
            return tcs.Task;
        }

        private void LogProcessStart(Process process)
        {
            logger.LogDebug($"Launching process {ProcessToString(process)}");
        }

        private static string ProcessToString(Process process)
        {
            var arguments = string.IsNullOrWhiteSpace(process.StartInfo.Arguments)
                ? string.Join(" ", process.StartInfo.ArgumentList.Select(a => $"\"{a}\""))
                : process.StartInfo.Arguments;
            return $"\"{process.StartInfo.FileName} " +
                   $"{arguments}\"";
        }
    }
}
