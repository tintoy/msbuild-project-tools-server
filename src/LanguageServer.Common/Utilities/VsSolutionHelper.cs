using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
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
        /// </param>
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> to use for diagnostic purposes (if not specified, the static <see cref="Log.Logger"/> will be used).
        /// </param>
        /// <returns>
        ///     The configured <see cref="ProjectCollection"/>
        /// </returns>
        /// <exception cref="AggregateException">
        ///     One or more projects failed to load, and <paramref name="throwOnProjectLoadFailure"/> is <c>true</c>.
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

        /// <summary>
        ///     Determine the likely format of the specified solution file.
        /// </summary>
        /// <param name="solutionFile">
        ///     The name of the solution file (including extension).
        /// </param>
        /// <returns>
        ///     A <see cref="VsSolutionFormat"/> value indicating the solution-file format.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///     <paramref name="solutionFile"/> is not a valid file name (with a recognised extension).
        /// </exception>
        public static VsSolutionFormat GetSolutionFormat(string solutionFile)
        {
            if (String.IsNullOrWhiteSpace(solutionFile))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'solutionFile'.", nameof(solutionFile));

            string? solutionFileExtension = Path.GetExtension(solutionFile);
            if (String.IsNullOrWhiteSpace(solutionFileExtension))
                throw new ArgumentException($"Cannot determine the solution file format (file name '{solutionFile}' has no extension).", nameof(solutionFile));

            return solutionFileExtension?.ToLowerInvariant() switch
            {
                ".sln"  => VsSolutionFormat.Legacy,
                ".slnx" => VsSolutionFormat.Xml,
                _       => VsSolutionFormat.Unknown,
            };
        }

        /// <summary>
        ///     Get an a appropriate solution serialiser for the specified solution-file format.
        /// </summary>
        /// <param name="format">
        ///     A <see cref="VsSolutionFormat"/> value indicating the solution-file format.
        /// </param>
        /// <returns>
        ///     The <see cref="ISolutionSerializer"/>, or <c>null</c> if the <paramref name="format"/> is not supported.
        /// </returns>
        public static ISolutionSerializer? GetSolutionSerializer(VsSolutionFormat format)
        {
            return format switch
            {
                VsSolutionFormat.Legacy => SolutionSerializers.SlnFileV12,
                VsSolutionFormat.Xml    => SolutionSerializers.SlnXml,
                _                       => null,
            };
        }
    }
}
