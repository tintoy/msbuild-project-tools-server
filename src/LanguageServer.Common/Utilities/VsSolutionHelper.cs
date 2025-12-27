using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Serilog;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Helper methods for working with <see cref="VsSolution"/>s.
    /// </summary>
    public static class VsSolutionHelper
    {
        /// <summary>
        ///     Create an MSBuild <see cref="ProjectCollection"/> with the projects in the specified <see cref="VsSolution"/>.
        /// </summary>
        /// <param name="solution">
        ///     The <see cref="VsSolution"/> containing projects to load.
        /// </param>
        /// <param name="globalPropertyOverrides">
        ///     An optional dictionary containing property values to override.
        /// </param>
        /// <param name="throwOnProjectLoadFailure">
        ///     Propagate project-load exceptions, if one or more projects fail to load?
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> to use for diagnostic purposes (if not specified, the static <see cref="Log.Logger"/> will be used).
        /// </param>
        /// <returns>
        ///     The configured <see cref="ProjectCollection"/>
        /// </returns>
        /// <exception cref="AggregateException">
        ///     One or more projects failed to load, and <see cref="throwOnProjectLoadFailure"/> is <c>true</c>.
        /// </exception>
        public static ProjectCollection CreateMSBuildProjectCollection(this VsSolution solution, Dictionary<string, string>? globalPropertyOverrides = null, bool throwOnProjectLoadFailure = false, ILogger? logger = null)
        {
            if (solution == null)
                throw new ArgumentNullException(nameof(solution));

            string? solutionDirectory = solution.File.Directory?.FullName;

            ProjectCollection projectCollection = MSBuildHelper.CreateProjectCollection(solutionDirectory, globalPropertyOverrides, logger);

            List<Exception>? projectLoadFailures = null;

            foreach (SolutionProjectModel projectModel in solution.Model.SolutionProjects)
            {
                try
                {
                    projectCollection.LoadProject(projectModel.FilePath);
                }
                catch (Exception projectLoadFailed)
                {
                    logger?.Error(projectLoadFailed, "Failed to load project {ProjectFile} into the MSBuild project collection for solution {SolutionFile}.", projectModel.FilePath, solution.File.FullName);

                    if (throwOnProjectLoadFailure)
                    {
                        if (projectLoadFailures == null)
                            projectLoadFailures = new List<Exception>();

                        projectLoadFailures.Add(projectLoadFailed);
                    }
                }
            }

            if (projectLoadFailures != null && projectLoadFailures.Count > 0)
                throw new AggregateException($"Unable to load one or more projects for solution '{solution.File.FullName}'.", projectLoadFailures);

            return projectCollection;
        }
    }
}