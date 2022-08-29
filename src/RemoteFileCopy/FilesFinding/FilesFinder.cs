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
        private const string ShaHashFunction = "$(sha1sum < $f | awk '{{ print $1 }}')";
        private const string SimpleHashFunction = "$({ head -c 4096 $f ; tail -c 1073741824 $f ; } | hexdump -e '16/1 \"%02x\"')";
        private static readonly Regex s_fileInfo = new(@"f(.+) l(\d+) c(.+)");
        private readonly SshWrapper _sshWrapper;
        private readonly ILogger<FilesFinder> _logger;

        public FilesFinder(SshWrapper sshWrapper, ILogger<FilesFinder> logger)
        {
            _sshWrapper = sshWrapper;
            _logger = logger;
        }

        public async Task<IEnumerable<RemoteFileInfo>> FindFiles(RemoteDir dir,
            CancellationToken cancellationToken = default)
        {
            var sshResult = await _sshWrapper.InvokeSshProcess(dir.Address, $"bash << {GetBashHereDoc(dir.Path, ShaHashFunction)}", cancellationToken);

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

        internal static string GetBashHereDoc(string path, string hashFunction)
        {
            return $@"'EOF'
if [ -d {path} ]
then 
    for f in $(find {path} -type f -print) ; do
        hash={hashFunction}
        size=$(stat -c%s $f)
        echo f$f l$size c$hash
    done
fi
EOF";
        }
    }
}
