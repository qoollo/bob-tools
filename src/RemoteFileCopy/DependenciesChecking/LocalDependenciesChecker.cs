using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteFileCopy.DependenciesChecking
{
    public class LocalDependenciesChecker
    {
        public async Task<bool> ProgramExists(string name, CancellationToken cancellationToken = default)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                }
            };

            process.StartInfo.ArgumentList.Add(name);

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0;
        }
    }
}