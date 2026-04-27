using Microsoft.Language.Xml;
using MSBuildProjectTools.LanguageServer.SemanticModel;
using MSBuildProjectTools.LanguageServer.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     Helper methods for loading test solutions.
    /// </summary>
    public static class TestSolutions
    {
        /// <summary>
        ///     Load a test solution into the solution collection.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="relativePathSegments">
        ///     The solution file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The loaded solution, as a <see cref="TestSolution"/>.
        /// </returns>
        public static ValueTask<TestSolution> LoadTestSolution<TTestClass>(params string[] relativePathSegments)
            where TTestClass : class
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            Type testClassType = typeof(TTestClass);

            return LoadTestSolution(testClassType, relativePathSegments);
        }

        /// <summary>
        ///     Load a test solution into the solution collection.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The solution file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The loaded solution, as a <see cref="TestSolution"/>.
        /// </returns>
        public static async ValueTask<TestSolution> LoadTestSolution(Type testClassType, params string[] relativePathSegments)
        {
            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string solutionFileName = GetSolutionFile(testClassType, relativePathSegments);

            VsSolution solution = await VsSolution.Load(solutionFileName);

            string solutionFileContent = File.ReadAllText(solutionFileName);
            XmlDocumentSyntax solutionXml = Parser.ParseText(solutionFileContent);

            var xmlPositions = new TextPositions(solutionFileContent);
            var xmlLocator = new XmlLocator(solutionXml, xmlPositions);

            var vsSolutionObjectLocator = new VsSolutionObjectLocator(solution, xmlLocator, xmlPositions);

            return new TestSolution(solution, vsSolutionObjectLocator, solutionXml, xmlLocator, xmlPositions);
        }

        /// <summary>
        ///     Load a test solution's XML.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="relativePathSegments">
        ///     The solution file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The loaded solution, as <see cref="TestSolutionXml"/>.
        /// </returns>
        public static TestSolutionXml LoadXml<TTestClass>(params string[] relativePathSegments)
            where TTestClass : class
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            Type testClassType = typeof(TTestClass);

            return LoadXml(testClassType, relativePathSegments);
        }

        /// <summary>
        ///     Load a test solution's XML.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The solution file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The loaded solution, as <see cref="TestSolutionXml"/>.
        /// </returns>
        public static TestSolutionXml LoadXml(Type testClassType, params string[] relativePathSegments)
        {
            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string solutionFileName = GetSolutionFile(testClassType, relativePathSegments);

            string solutionFileContent = File.ReadAllText(solutionFileName);
            XmlDocumentSyntax solutionXml = Parser.ParseText(solutionFileContent);

            var xmlPositions = new TextPositions(solutionFileContent);
            var xmlLocator = new XmlLocator(solutionXml, xmlPositions);

            return new TestSolutionXml(solutionXml, xmlLocator, xmlPositions);
        }

        /// <summary>
        ///     Get the fully-qualified path of a test solution file.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="relativePathSegments">
        ///     The solution file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The full path to the solution file.
        /// </returns>
        public static string GetSolutionFile<TTestClass>(params string[] relativePathSegments)
            where TTestClass : class
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            Type testClassType = typeof(TTestClass);

            return GetSolutionFile(testClassType, relativePathSegments);
        }

        /// <summary>
        ///     Get the fully-qualified path of a test solution file.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The solution file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The full path to the solution file.
        /// </returns>
        public static string GetSolutionFile(Type testClassType, params string[] relativePathSegments)
        {
            if (testClassType == null)
                throw new ArgumentNullException(nameof(testClassType));

            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string testDeploymentDirectory = GetTestDeploymentDirectory(testClassType);

            string solutionFileName = Path.Combine([
                testDeploymentDirectory,
                .. relativePathSegments
            ]);
            solutionFileName = Path.GetFullPath(solutionFileName); // Flush out any relative-path issues now, rather than when we are trying to open the file.

            return solutionFileName;
        }

        /// <summary>
        ///     Get the fully-qualified path of a test solution file.
        /// </summary>
        /// <typeparam name="TTestClass">
        ///     The test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </typeparam>
        /// <param name="relativePathSegments">
        ///     The solution file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The full path to the solution file.
        /// </returns>
        public static string GetSolutionDirectory<TTestClass>(params string[] relativePathSegments)
            where TTestClass : class
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string solutionFileName = GetSolutionFile<TTestClass>(relativePathSegments);

            return Path.GetDirectoryName(solutionFileName);
        }

        /// <summary>
        ///     Get the fully-qualified path of a test solution file.
        /// </summary>
        /// <param name="testClassType">
        ///     The type of test-suite class used to determine the assembly containing the currently-running test (which can be used to find the test's deployment directory).
        /// </param>
        /// <param name="relativePathSegments">
        ///     The solution file's relative (to the test deployment directory) path segments.
        /// </param>
        /// <returns>
        ///     The full path to the solution file.
        /// </returns>
        public static string GetSolutionDirectory(Type testClassType, params string[] relativePathSegments)
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            string solutionFileName = GetSolutionFile(testClassType, relativePathSegments);

            return Path.GetDirectoryName(solutionFileName);
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
    ///     An VsSolution solution, loaded for a test.
    /// </summary>
    /// <param name="SolutionXml">
    ///     An <see cref="XmlDocumentSyntax"/> containing the solution XML.
    /// </param>
    /// <param name="XmlLocations">
    ///     The <see cref="XmlLocator"/> for the solution XML.
    /// </param>
    /// <param name="TextPositions">
    ///     The <see cref="TextPositions"/> for the solution text.
    /// </param>
    public record class TestSolutionXml(
        XmlDocumentSyntax SolutionXml,
        XmlLocator XmlLocations,
        TextPositions TextPositions
    );

    /// <summary>
    ///     A solution, loaded for a test.
    /// </summary>
    /// <param name="Solution">
    ///     The underlying VsSolution <see cref="Solution"/>.
    /// </param>
    /// <param name="ObjectLocations">
    ///     The <see cref="VsSolutionObjectLocator"/> for the solution.
    /// </param>
    /// <param name="SolutionXml">
    ///     An <see cref="XmlDocumentSyntax"/> containing the solution XML.
    /// </param>
    /// <param name="XmlLocations">
    ///     The <see cref="XmlLocator"/> for the solution XML.
    /// </param>
    /// <param name="TextPositions">
    ///     The <see cref="TextPositions"/> for the solution text.
    /// </param>
    public record class TestSolution(
        VsSolution Solution,
        VsSolutionObjectLocator ObjectLocations,
        XmlDocumentSyntax SolutionXml,
        XmlLocator XmlLocations,
        TextPositions TextPositions
    )
        : TestSolutionXml(SolutionXml, XmlLocations, TextPositions);
}
