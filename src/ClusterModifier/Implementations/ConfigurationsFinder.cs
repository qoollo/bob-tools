using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobToolsCli.Exceptions;

namespace ClusterModifier;

public class ConfigurationsFinder : IConfigurationsFinder
{
    private readonly ClusterExpandArguments _args;

    public ConfigurationsFinder(ClusterExpandArguments args)
    {
        _args = args;
    }

    public async Task<ClusterConfiguration> FindOldConfig(CancellationToken cancellationToken)
    {
        var oldConfigResult = await _args.GetClusterConfigurationFromFile(
            _args.OldConfigPath,
            cancellationToken
        );
        if (!oldConfigResult.IsOk(out var oldConfig, out var oldError))
            throw new ConfigurationException($"Old config is not available: {oldError}");
        return oldConfig;
    }

    public async Task<ClusterConfiguration> FindNewConfig(CancellationToken cancellationToken)
    {
        var configResult = await _args.FindClusterConfiguration(
            cancellationToken: cancellationToken
        );
        if (!configResult.IsOk(out var config, out var newError))
            throw new ConfigurationException($"Current config is not available: {newError}");
        return config;
    }
}
