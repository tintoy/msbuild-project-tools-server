using Microsoft.Build.Evaluation;
using Microsoft.Language.Xml;
using MSBuildProjectTools.LanguageServer.SemanticModel;
using MSBuildProjectTools.LanguageServer.Utilities;
using Serilog;
using System;
using System.IO;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     Helper methods for loading test projects.
    /// </summary>
    public static class TestProjects
    {
        /// <summary>
        ///     Create an MSBuild <see cref="ProjectCollection"/> using the specified project directory.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> that will receives MSBuild logs.
        /// </param>
        /// <param name="relativeDirectoryPathSegments">
        ///     Path segments (if any) that will be appended to the test's deployment directory to specify the project directory.
        /// </param>
        /// <returns>
        ///     The configured <see cref="ProjectCollection"/>.
        /// </returns>
        public static ProjectCollection CreateProjectCollection<TTestClass>(params string[] relativePathSegments)
            where TTestClass : class
        {
            return CreateProjectCollection<TTestClass>(logger: null, relativePathSegments);
        }

        /// <summary>
        ///     Create an MSBuild <see cref="ProjectCollection"/> using the specified project directory.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> that will receives MSBuild logs.
        /// </param>
        /// <param name="relativePathSegments">
        ///     The target project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The configured <see cref="ProjectCollection"/>.
        /// </returns>
        public static ProjectCollection CreateProjectCollection<TTestClass>(ILogger logger, params string[] relativePathSegments)
            where TTestClass : class
        {
            string projectDirectory = GetProjectDirectory<TTestClass>(relativePathSegments);

            ProjectCollection projectCollection = MSBuildHelper.CreateProjectCollection(projectDirectory, logger: logger);

            return projectCollection;
        }

        /// <summary>
        ///     Create an MSBuild <see cref="ProjectCollection"/> using the specified project directory.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The target project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The configured <see cref="ProjectCollection"/>.
        /// </returns>
        public static ProjectCollection CreateProjectCollection(Type testClassType, params string[] relativePathSegments)
        {
            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            return CreateProjectCollection(testClassType, logger: null, relativePathSegments);
        }

        /// <summary>
        ///     Create an MSBuild <see cref="ProjectCollection"/> using the specified project directory.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> that will receives MSBuild logs.
        /// </param>
        /// <param name="relativePathSegments">
        ///     The target project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The configured <see cref="ProjectCollection"/>.
        /// </returns>
        public static ProjectCollection CreateProjectCollection(Type testClassType, ILogger logger, params string[] relativePathSegments)
        {
            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            string projectDirectory = GetProjectDirectory(testClassType, relativePathSegments);

            ProjectCollection projectCollection = MSBuildHelper.CreateProjectCollection(projectDirectory, logger: logger);

            return projectCollection;
        }

        /// <summary>
        ///     Load a test project into the project collection.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="relativePathSegments">
        ///     The project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The loaded project, as a <see cref="TestProject"/>.
        /// </returns>
        public static TestProject LoadTestProject<TTestClass>(this ProjectCollection projectCollection, params string[] relativePathSegments)
            where TTestClass : class
        {
            if (projectCollection == null)
                throw new ArgumentNullException(nameof(projectCollection));

            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            Type testClassType = typeof(TTestClass);

            return projectCollection.LoadTestProject(testClassType, relativePathSegments);
        }

        /// <summary>
        ///     Load a test project into the project collection.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The loaded project, as a <see cref="TestProject"/>.
        /// </returns>
        public static TestProject LoadTestProject(this ProjectCollection projectCollection, Type testClassType, params string[] relativePathSegments)
        {
            if (projectCollection == null)
                throw new ArgumentNullException(nameof(projectCollection));

            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string projectFileName = GetProjectFile(testClassType, relativePathSegments);

            string projectFileContent = File.ReadAllText(projectFileName);
            XmlDocumentSyntax projectXml = Parser.ParseText(projectFileContent);

            var xmlPositions = new TextPositions(projectFileContent);
            var xmlLocator = new XmlLocator(projectXml, xmlPositions);

            Project msbuildProject = projectCollection.GetLoadedProjects(projectFileName).FirstOrDefault();
            if (msbuildProject == null)
                msbuildProject = projectCollection.LoadProject(projectFileName);

            var msbuildObjectLocator = new MSBuildObjectLocator(msbuildProject, xmlLocator, xmlPositions);

            return new TestProject(msbuildProject, msbuildObjectLocator, projectXml, xmlLocator, xmlPositions);
        }

        /// <summary>
        ///     Load a test project's XML.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="relativePathSegments">
        ///     The project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The loaded project, as <see cref="TestProjectXml"/>.
        /// </returns>
        public static TestProjectXml LoadXml<TTestClass>(params string[] relativePathSegments)
            where TTestClass : class
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            Type testClassType = typeof(TTestClass);

            return LoadXml(testClassType, relativePathSegments);
        }

        /// <summary>
        ///     Load a test project's XML.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The loaded project, as <see cref="TestProjectXml"/>.
        /// </returns>
        public static TestProjectXml LoadXml(Type testClassType, params string[] relativePathSegments)
        {
            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string projectFileName = GetProjectFile(testClassType, relativePathSegments);

            string projectFileContent = File.ReadAllText(projectFileName);
            XmlDocumentSyntax projectXml = Parser.ParseText(projectFileContent);

            var xmlPositions = new TextPositions(projectFileContent);
            var xmlLocator = new XmlLocator(projectXml, xmlPositions);

            return new TestProjectXml(projectXml, xmlLocator, xmlPositions);
        }

        /// <summary>
        ///     Get the fully-qualified path of a test project file.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="relativePathSegments">
        ///     The project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The full path to the project file.
        /// </returns>
        public static string GetProjectFile<TTestClass>(params string[] relativePathSegments)
            where TTestClass : class
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            Type testClassType = typeof(TTestClass);

            return GetProjectFile(testClassType, relativePathSegments);
        }

        /// <summary>
        ///     Get the fully-qualified path of a test project file.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The full path to the project file.
        /// </returns>
        public static string GetProjectFile(Type testClassType, params string[] relativePathSegments)
        {
            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string testDeploymentDirectory = GetTestDeploymentDirectory(testClassType);

            string projectFileName = Path.Combine([
                testDeploymentDirectory,
                .. relativePathSegments
            ]);
            projectFileName = Path.GetFullPath(projectFileName); // Flush out any relative-path issues now, rather than when we are trying to open the file.

            return projectFileName;
        }

        /// <summary>
        ///     Get the fully-qualified path of a test project file.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="relativePathSegments">
        ///     The project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The full path to the project file.
        /// </returns>
        public static string GetProjectDirectory<TTestClass>(params string[] relativePathSegments)
            where TTestClass : class
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string projectFileName = GetProjectFile<TTestClass>(relativePathSegments);

            return Path.GetDirectoryName(projectFileName);
        }

        /// <summary>
        ///     Get the fully-qualified path of a test project file.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The project file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The full path to the project file.
        /// </returns>
        public static string GetProjectDirectory(Type testClassType, params string[] relativePathSegments)
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string projectFileName = GetProjectFile(testClassType, relativePathSegments);

            return Path.GetDirectoryName(projectFileName);
        }

        /// <summary>
        ///     Get the deployment directory for the specified test class.
        /// </summary>
        /// <param name="testClassType">
        ///     The test class <see cref="Type"/>.
        /// </param>
        /// <returns>
        ///     The test deployment directory.
        /// </returns>
        static string GetTestDeploymentDirectory(Type testClassType)
        {
            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            string testAssemblyFile = testClassType.Assembly.Location;
            
            return Path.GetDirectoryName(testAssemblyFile);
        }
    }

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
    public record class TestProjectXml(
        XmlDocumentSyntax ProjectXml,
        XmlLocator XmlLocations,
        TextPositions TextPositions
    );

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
    public record class TestProject(
        Project MSBuildProject,
        MSBuildObjectLocator ObjectLocations,
        XmlDocumentSyntax ProjectXml,
        XmlLocator XmlLocations,
        TextPositions TextPositions
    )
        : TestProjectXml(ProjectXml, XmlLocations, TextPositions);
}
