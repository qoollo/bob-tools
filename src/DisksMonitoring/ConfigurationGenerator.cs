using System;
using System.Threading;
using System.Threading.Tasks;
using BobApi;
using BobToolsCli.Exceptions;
using DisksMonitoring.Config;
using Microsoft.Extensions.Logging;

namespace DisksMonitoring;

class ConfigurationGenerator
{
    private readonly Configuration _configuration;
    private readonly ILogger<ConfigurationGenerator> _logger;

    public ConfigurationGenerator(
        Configuration configuration,
        ILogger<ConfigurationGenerator> logger
    )
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Configuration> Generate(
        string stateFile,
        BobApiClient bobApiClient,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Reading configuration...");
        try
        {
            await _configuration.ReadFromFile(stateFile);
            await _configuration.AddEntriesFromBob(bobApiClient);
            await _configuration.SaveToFile(stateFile);
            return _configuration;
        }
        catch (Exception e)
        {
            _logger.LogError(
                $"Exception while getting info from config: {e.Message}{Environment.NewLine}{e.StackTrace}"
            );
            throw;
        }
        finally
        {
            _logger.LogInformation("Reading configuration done");
        }
    }
}
