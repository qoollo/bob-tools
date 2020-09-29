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
    public class RsyncWrapper : IEquatable<RsyncWrapper>
    {
        private const string syncedFilesFilename = "synced_files";
        private static readonly HashSet<string> ExcludedFiles = new HashSet<string> { "*synced_files", "*lock" };
        private static readonly HashSet<string> ForbiddenNames = new HashSet<string> {"."};
        private readonly string rsyncCmd;
        private readonly string sshCmd;
        private readonly string pathToSshKey;
        private readonly string username;
        private readonly int sshPort;
        private readonly Uri hostUri;
        private readonly Uri innerNetworkUri;
        private readonly Configuration configuration;
        private readonly ILogger<RsyncWrapper> logger;

        public RsyncWrapper(int sshPort, Uri hostUri, Uri innerNetworkUri, Configuration configuration,
            ILogger<RsyncWrapper> logger)
        {
            this.sshPort = sshPort;
            this.hostUri = hostUri;
            this.innerNetworkUri = innerNetworkUri;
            this.configuration = configuration;
            this.logger = logger;
            rsyncCmd = configuration.RsyncCmd;
            sshCmd = configuration.SshCmd;
            pathToSshKey = configuration.PathToSshKey;
            username = configuration.SshUsername;
        }

        public async Task<List<TreeParser.Entry>> GetDirectories(string baseDir)
        {
            var output = await InvokeSshCommandWithOutput($"tree {baseDir} -d");
            var parser = new TreeParser(baseDir, output, true);
            return parser.RootEntries;
        }

        public async Task<List<RsyncEntry>> GetDirs(string baseDir)
        {
            baseDir = baseDir.TrimEnd('/');
            var process = new Process {StartInfo = CreateRsyncForListOnly(baseDir), EnableRaisingEvents = true};
            LogProcessStart(process);
            var tcs = new TaskCompletionSource<List<RsyncEntry>>();
            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                    logger.LogError($"Process {ProcessToString(process)} failed, stderr: {process.StandardError.ReadToEnd()}");
                var lines = process.StandardOutput.ReadToEnd().Split('\n');
                var result = new List<RsyncEntry>(lines.Length);
                foreach (var line in lines)
                {
                    try
                    {
                        var entry = new RsyncEntry(line, baseDir, this);
                        if (!ForbiddenNames.Contains(entry.Name))
                            result.Add(entry);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                tcs.SetResult(result);
                process.Dispose();
            };
            process.Start();
            return await tcs.Task;
        }

        public async Task<bool> Copy(RsyncEntry from, RsyncEntry to)
        {
            var excludedFilesString = string.Join(' ', ExcludedFiles.Select(f => $"--exclude {f}"));
            var copyCommand = $"rsync -e 'ssh -o \"StrictHostKeyChecking=no\"' {excludedFilesString} " +
                              $"-av {from.Path}/ {configuration.SshUsername}@{to.RsyncWrapper.innerNetworkUri.Host}:{to.Path}";
            var copyResult = await from.RsyncWrapper.InvokeSshCommand(copyCommand);
            if (!copyResult) return false;
            
            return await SaveSyncedFiles(@from, to);
        }

        private async Task<bool> SaveSyncedFiles(RsyncEntry @from, RsyncEntry to)
        {
            var fromResult = await from.FindFilesWithSha();
            var toResult = await to.FindFilesWithSha();
            foreach (var line in fromResult)
                if (!toResult.Contains(line))
                {
                    logger.LogInformation($"File \"{line}\" not synced!");
                }

            fromResult.RemoveAll(line => !toResult.Contains(line));

            fromResult.RemoveAll(line => ExcludedFiles.Any(f => line.Contains(f.Trim('*'))));

            var syncedFilesFullFilename = $"{@from.Path}/{syncedFilesFilename}";
            var echoCommand = $"echo -e \"{string.Join("\n", fromResult)}\"" +
                              $" > {syncedFilesFullFilename}";

            return await @from.RsyncWrapper.InvokeSshCommand(echoCommand);
        }

        public static string GetListFilesWithShaCommand(string path)
        {
            return $"find {path} -type f -exec sha256sum {{}} \\;";
        }

        public static string GetSyncedFilesReadCommand(string path)
        {
            return $"cat {path}/{syncedFilesFilename}";
        }

        public static string GetRemoveFilesCommand(IEnumerable<string> files)
        {
            return $"rm -f {string.Join(' ', files)}";
        }

        public Task<bool> InvokeSshCommand(string command)
        {
            var process = GetSshProcess(command, false);
            LogProcessStart(process);
            var tcs = new TaskCompletionSource<bool>();
            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                    logger.LogError(
                        $"Process {ProcessToString(process)} failed, stderr:{Environment.NewLine}{process.StandardError.ReadToEnd()}");
                tcs.SetResult(process.ExitCode == 0);
                process.Dispose();
            };
            process.Start();
            return tcs.Task;
        }

        public Task<List<string>> InvokeSshCommandWithOutput(string command)
        {
            var process = GetSshProcess(command, true);
            LogProcessStart(process);
            var tcs = new TaskCompletionSource<List<string>>();
            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                    logger.LogError(
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

        private Process GetSshProcess(string command, bool redirectOutput)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = configuration.SshCmd,
                    ArgumentList =
                    {
                        $"-p {sshPort}",
                        $"{configuration.SshUsername}@{hostUri.Host}",
                        $"-i {configuration.PathToSshKey}",
                        command
                    },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput =  redirectOutput
                },
                EnableRaisingEvents = true,
            };
            return process;
        }

        private ProcessStartInfo CreateRsyncForListOnly(string baseDir)
        {
            return CreateRsync("--list-only", $"{username}@{hostUri.Host}:{baseDir}/");
        }

        private ProcessStartInfo CreateRsync(params string[] args)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = rsyncCmd,
                ArgumentList = { "-e", $"{sshCmd} -p {sshPort} -i {pathToSshKey}"},//$"-e '{sshCmd} -p {sshPort} -i {pathToSshKey}' {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var arg in args)
                processStartInfo.ArgumentList.Add(arg);                
            return processStartInfo;
        }

        public bool Equals(RsyncWrapper other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return sshPort == other.sshPort && Equals(hostUri, other.hostUri);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RsyncWrapper) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(sshPort, hostUri);
        }

        private void LogProcessStart(Process process)
        {
            logger.LogInformation($"Launching process {ProcessToString(process)}");
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
