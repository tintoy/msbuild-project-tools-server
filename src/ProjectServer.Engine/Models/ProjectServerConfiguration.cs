using System;

namespace MSBuildProjectTools.ProjectServer.Engine.Models
{
    public record class ProjectServerConfiguration(LoggingConfiguration Logging)
    {
        public static readonly ProjectServerConfiguration Default = new ProjectServerConfiguration(
            Logging: LoggingConfiguration.Default
        );

        public ProjectServerConfiguration Merge(ProjectServerConfiguration projectServerConfiguration)
        {
            if (projectServerConfiguration == null)
                throw new ArgumentNullException(nameof(projectServerConfiguration));

            if (Equals(projectServerConfiguration))
                return this;

            return this with
            {
                Logging = projectServerConfiguration.Logging,
            };
        }
    }
}
