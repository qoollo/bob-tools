using Microsoft.Extensions.DependencyInjection;
using RemoteFileCopy.RemoteFileCreation;

namespace RemoteFileCopy.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddRemoteFileCopy(this IServiceCollection services)
        {
            return services
                .AddScoped<RemoteFileCopier>()
                .AddScoped<RemoteFileCreator>();
        }
    }
}