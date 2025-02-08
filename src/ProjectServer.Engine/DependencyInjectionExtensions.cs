using Microsoft.Extensions.DependencyInjection;
using MSBuildProjectTools.ProjectServer.Engine;

namespace MSBuildProjectTools.ProjectServer
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddProjectServerEngine(this IServiceCollection services)
        {
            services.AddSingleton<IProjectServerEngine, ProjectServerEngine>();

            return services;
        }
    }
}
