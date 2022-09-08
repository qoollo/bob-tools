using System;
using BobToolsCli.Exceptions;
using System.Collections.Generic;
using CommandLine;
using RemoteFileCopy.FilesFinding;
using RemoteFileCopy.Ssh;

namespace BobToolsCli
{
    public class CommonWithSshArguments : CommonArguments
    {
        [Option("ssh-cmd", HelpText = "Ssh cmd.", Default = "ssh")]
        public string SshCmd { get; set; }

        [Option("ssh-flags", HelpText = "Additional flags to pass to ssh. Without spaces, without dash, delimeted by comma. Example: T,x", Separator = ',')]
        public IEnumerable<string> SshFlags { get; set; } = Array.Empty<string>();

        [Option("ssh-port", HelpText = "Ssh port.", Default = 22)]
        public int SshPort { get; set; }

        [Option("ssh-user", HelpText = "Ssh username.", Default = "bobd")]
        public string SshUser { get; set; }

        [Option("ssh-key-path", HelpText = "Path to ssh key.", Default = "~/.ssh/id_rsa")]
        public string SshKeyPath { get; set; }

        [Option("hash-type", HelpText = "Hash type. Available options are simple, sha", Default = "sha")]
        public string HashTypeString { get; set; }

        public SshConfiguration SshConfiguration => new(SshCmd, SshPort, SshUser, SshKeyPath);

        public FilesFinderConfiguration FilesFinderConfiguration =>
            Enum.TryParse<HashType>(HashTypeString, true, out var ht) ? new(ht) : throw new ConfigurationException("Failed to parse hash-type");
    }
}
