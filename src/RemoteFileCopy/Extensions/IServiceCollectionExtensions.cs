using Microsoft.Extensions.DependencyInjection;
using RemoteFileCopy.DependenciesChecking;
using RemoteFileCopy.FilesFinding;
using RemoteFileCopy.Rsync;
using RemoteFileCopy.Ssh;

namespace RemoteFileCopy.Extensions
{
    public static class IServiceCollectionExtensions
    {
        private static readonly SshConfiguration s_defaultSshConfiguration = new("ssh", 22, "bobd", "~/.ssh/id_rsa");
        private static readonly FilesFinderConfiguration s_defaultFilesFinderConfiguration = new(HashType.Sha);

        public static IServiceCollection AddRemoteFileCopy(this IServiceCollection services,
                                                           SshConfiguration? sshConfiguration = null,
        FilesFinderConfiguration? filesFinderConfiguration = null)
        {
            sshConfiguration ??= s_defaultSshConfiguration;
            filesFinderConfiguration ??= s_defaultFilesFinderConfiguration;
            services.AddSingleton(sshConfiguration);
            return services
                .AddScoped<RsyncRemoteFileCopier>()
                .AddScoped<IRemoteFileCopier>(s => new LocalOptimizedRemoteFileCopier(s.GetService<RsyncRemoteFileCopier>()!))
                .AddScoped<SshWrapper>()
                .AddScoped<RsyncWrapper>()
                .AddScoped<FilesFinder>()
                .AddScoped<LocalDependenciesChecker>()
                .AddScoped<RemoteDependenciesChecker>();
        }
    }
}
