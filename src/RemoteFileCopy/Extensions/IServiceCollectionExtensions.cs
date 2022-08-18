using System;
using Microsoft.Extensions.DependencyInjection;
using RemoteFileCopy.DependenciesChecking;
using RemoteFileCopy.FilesFinding;
using RemoteFileCopy.Rsync;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy.Extensions
{
    public static class IServiceCollectionExtensions
    {
        private static readonly SshConfiguration s_defaultSshConfiguration = new("ssh", Array.Empty<string>(), 22, "bobd", "~/.ssh/id_rsa");

        public static IServiceCollection AddRemoteFileCopy(this IServiceCollection services,
            SshConfiguration? sshConfiguration = null)
        {
            sshConfiguration ??= s_defaultSshConfiguration;
            services.AddSingleton(sshConfiguration);
            return services
                .AddScoped<RemoteFileCopier>()
                .AddScoped<SshWrapper>()
                .AddScoped<RsyncWrapper>()
                .AddScoped<FilesFinder>()
                .AddScoped<LocalDependenciesChecker>()
                .AddScoped<RemoteDependenciesChecker>();
        }
    }
}
