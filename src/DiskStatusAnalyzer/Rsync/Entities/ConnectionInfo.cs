using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BobApi.Entities;

namespace DiskStatusAnalyzer.Rsync.Entities
{
    public class ConnectionInfo
    {
        public ConnectionInfo(Uri uri,
                                            int sshPort,
                                            Uri innerUri,
                                            Configuration configuration)
        {
            Uri = uri;
            SshPort = sshPort;
            InnerUri = innerUri;
            RsyncCmd = configuration.RsyncCmd;
            SshCmd = configuration.SshCmd;
            PathToSshKey = configuration.PathToSshKey;
            SshUsername = configuration.SshUsername;
        }

        public Uri Uri { get; }
        public int SshPort { get; }
        public Uri InnerUri { get; }
        public string RsyncCmd { get; }
        public string SshCmd { get; }
        public string PathToSshKey { get; }
        public string SshUsername { get; }

        public Process GetSshProcess(string command, bool redirectOutput)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = SshCmd,
                    ArgumentList =
                    {
                        $"-p {SshPort}",
                        $"{SshUsername}@{Uri.Host}",
                        $"-i {PathToSshKey}",
                        command
                    },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = redirectOutput
                },
                EnableRaisingEvents = true,
            };
            return process;
        }
    }
}
