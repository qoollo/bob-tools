using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DisksMonitoring.Bob;
using DisksMonitoring.Config;
using DisksMonitoring.OS;
using Microsoft.Extensions.Logging;

namespace DisksMonitoring;

class Monitor
{
    private readonly MonitorArguments _args;
    private readonly DisksMonitor _disksMonitor;
    private readonly ExternalScriptsRunner _externalScriptsRunner;
    private readonly DisksStarter _disksStarter;
    private readonly ILogger<Monitor> _logger;

    public Monitor(
        MonitorArguments args,
        DisksMonitor disksMonitor,
        ExternalScriptsRunner externalScriptsRunner,
        DisksStarter disksStarter,
        ILogger<Monitor> logger
    )
    {
        _args = args;
        _disksMonitor = disksMonitor;
        _externalScriptsRunner = externalScriptsRunner;
        _disksStarter = disksStarter;
        _logger = logger;
    }

    public async Task Run(Configuration configuration, CancellationToken cancellationToken)
    {
        var span = TimeSpan.FromSeconds(configuration.MinCycleTimeSec);
        var client = await _args.GetLocalBobClient(cancellationToken);
        _logger.LogInformation("Start monitor");
        while (true)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await _externalScriptsRunner.RunPreCycleScripts();
                await _disksMonitor.CheckAndUpdate(client);
                await configuration.SaveToFile(_args.StateFile);
                await _disksStarter.StartDisks(client);
                await _externalScriptsRunner.RunPostCycleScripts();
            }
            catch (Exception e)
            {
                _logger.LogError(
                    $"Exception while processing cycle: {e.Message}{Environment.NewLine}{e.StackTrace}"
                );
            }
            sw.Stop();
            if (sw.Elapsed < span)
                await Task.Delay(span - sw.Elapsed);
        }
    }
}
