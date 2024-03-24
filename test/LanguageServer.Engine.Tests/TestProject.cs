using Microsoft.Build.Evaluation;
using Microsoft.Language.Xml;
using MSBuildProjectTools.LanguageServer.SemanticModel;
using MSBuildProjectTools.LanguageServer.Utilities;
using Serilog;
using System;
using System.IO;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     An MSBuild project, loaded for a test.
    /// </summary>
    /// <param name="MSBuildProject">
    ///     The underlying MSBuild <see cref="Project"/>.
    /// </param>
    /// <param name="ObjectLocations">
    ///     The <see cref="MSBuildObjectLocator"/> for the project.
    /// </param>
    /// <param name="ProjectXml">
    ///     An <see cref="XmlDocumentSyntax"/> containing the project XML.
    /// </param>
    /// <param name="XmlLocations">
    ///     The <see cref="XmlLocator"/> for the project XML.
    /// </param>
    /// <param name="TextPositions">
    ///     The <see cref="TextPositions"/> for the project text.
    /// </param>
    public record class TestProject(Project MSBuildProject, MSBuildObjectLocator ObjectLocations, XmlDocumentSyntax ProjectXml, XmlLocator XmlLocations, TextPositions TextPositions)
        : TestProjectXml(ProjectXml, XmlLocations, TextPositions)
    {
        /// <summary>
        ///     Load a test project.
        /// </summary>
        /// <param name="relativePathSegments">
        ///     The file's relative path segments.
        /// </param>
        /// <returns>
        ///     The loaded project, as a <see cref="TestProject"/>.
        /// </returns>
        public static TestProject Load(ref ProjectCollection projectCollection, ILogger logger, params string[] relativePathSegments)
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string projectFileName = Path.Combine(relativePathSegments);
            string projectDirectory = Path.GetDirectoryName(projectFileName);

            if (projectCollection == null)
                projectCollection = MSBuildHelper.CreateProjectCollection(projectDirectory, logger: logger);

            string projectFileContent = File.ReadAllText(projectFileName);
            XmlDocumentSyntax projectXml = Parser.ParseText(projectFileContent);

            var xmlPositions = new TextPositions(projectFileContent);
            var xmlLocator = new XmlLocator(projectXml, xmlPositions);

            Project msbuildProject = projectCollection.LoadProject(projectFileName);
            var msbuildObjectLocator = new MSBuildObjectLocator(msbuildProject, xmlLocator, xmlPositions);

            return new TestProject(msbuildProject, msbuildObjectLocator, projectXml, xmlLocator, xmlPositions);
        }

        /// <summary>
        ///     Load a test project's XML.
        /// </summary>
        /// <param name="relativePathSegments">
        ///     The file's relative path segments.
        /// </param>
        /// <returns>
        ///     The loaded project, as <see cref="TestProjectXml"/>.
        /// </returns>
        public static TestProjectXml LoadXml(params string[] relativePathSegments)
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string projectFileName = Path.Combine(relativePathSegments);

            string projectFileContent = File.ReadAllText(projectFileName);
            XmlDocumentSyntax projectXml = Parser.ParseText(projectFileContent);

            var xmlPositions = new TextPositions(projectFileContent);
            var xmlLocator = new XmlLocator(projectXml, xmlPositions);

            return new TestProjectXml(projectXml, xmlLocator, xmlPositions);
        }
    };

    /// <summary>
    ///     An MSBuild project, loaded for a test.
    /// </summary>
    /// <param name="ProjectXml">
    ///     An <see cref="XmlDocumentSyntax"/> containing the project XML.
    /// </param>
    /// <param name="XmlLocations">
    ///     The <see cref="XmlLocator"/> for the project XML.
    /// </param>
    /// <param name="TextPositions">
    ///     The <see cref="TextPositions"/> for the project text.
    /// </param>
    public record class TestProjectXml(XmlDocumentSyntax ProjectXml, XmlLocator XmlLocations, TextPositions TextPositions);
}
