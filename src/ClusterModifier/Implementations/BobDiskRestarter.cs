using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ClusterModifier;

public class BobDiskRestarter : IBobDiskRestarter
{
    private readonly ClusterExpandArguments _args;
    private readonly ILogger<BobDiskRestarter> _logger;

    public BobDiskRestarter(ClusterExpandArguments args, ILogger<BobDiskRestarter> logger)
    {
        _args = args;
        _logger = logger;
    }

    public async Task RestartDisk(NodeDisk disk, CancellationToken cancellationToken)
    {
        var provider = _args.GetBobApiClientProvider();
        using var api = provider.GetClient(disk.Node);

        var result = await api.RestartDisk(disk.DiskName, cancellationToken);
        if (result.IsOk(out var success, out var e))
        {
            if (!success)
                _logger.LogError(
                    "Failed to restart disk {Disk} on node {Node}",
                    disk.DiskName,
                    disk.Node.Name
                );
        }
        else
            _logger.LogError(
                "Error while restarting disk {Disk} on node {Node}: {Error}",
                disk.DiskName,
                disk.Node.Name,
                e
            );
    }
}
