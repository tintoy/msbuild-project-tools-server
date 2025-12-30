using Serilog;
using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders.Project
{
    using Documents;
    using SemanticModel;

    /// <summary>
    ///     Base class for MSBuild task completion providers.
    /// </summary>
    public abstract class TaskCompletionProvider
        : CompletionProvider<ProjectDocument>
    {
        /// <summary>
        ///     Create a new <see cref="TaskCompletionProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected TaskCompletionProvider(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     Get all tasks defined in the project.
        /// </summary>
        /// <param name="projectDocument">
        ///     The project document.
        /// </param>
        /// <returns>
        ///     A dictionary of task metadata, keyed by task name.
        /// </returns>
        protected static Dictionary<string, MSBuildTaskMetadata> GetProjectTasks(ProjectDocument projectDocument)
        {
            ArgumentNullException.ThrowIfNull(projectDocument);

            // We trust that all tasks discovered via GetMSBuildProjectTaskAssemblies are accessible in the current project.

            var tasks = new Dictionary<string, MSBuildTaskMetadata>();
            foreach (MSBuildTaskAssemblyMetadata assemblyMetadata in projectDocument.GetMSBuildProjectTaskAssemblies())
            {
                foreach (MSBuildTaskMetadata task in assemblyMetadata.Tasks)
                    tasks[task.Name] = task;
            }

            return tasks;
        }
    }
}
