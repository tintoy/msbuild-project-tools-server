using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    using NuGet.Versioning;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Tests for MSBuild integration.
    /// </summary>
    public class MSBuildTests
        : TestBase
    {
        /// <summary>
        ///     The directory for test files.
        /// </summary>
        static readonly DirectoryInfo TestDirectory = new DirectoryInfo(Path.GetDirectoryName(
            new Uri(typeof(XmlLocatorTests).Assembly.CodeBase).LocalPath
        ));

        /// <summary>
        ///     Create a new MSBuild integration test suite.
        /// </summary>
        /// <param name="testOutput">
        ///     Output for the current test.
        /// </param>
        public MSBuildTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        // TODO: More realistic tests for working with MSBuild evaluation projects.

        /// <summary>
        ///     Dump all UsingTask elements in an MSBuild project.
        /// </summary>
        [InlineData("Project1")]
        [Theory(DisplayName = "Dump all UsingTask elements in an MSBuild project ")]
        public void DumpUsingTasks(string projectName)
        {
            Project project = LoadTestProject(projectName + ".csproj");
            using (project.ProjectCollection)
            {
                foreach (ProjectUsingTaskElement usingTaskElement in project.GetAllUsingTasks())
                {
                    TestOutput.WriteLine("UsingTask '{0}' from '{1}':",
                        usingTaskElement.TaskName,
                        usingTaskElement.ContainingProject.FullPath
                    );
                    TestOutput.WriteLine("\tAssemblyFile: '{0}'",
                        project.ExpandString(usingTaskElement.AssemblyFile)
                    );
                    TestOutput.WriteLine("\tAssemblyName: '{0}'",
                        project.ExpandString(usingTaskElement.AssemblyName)
                    );
                    TestOutput.WriteLine("\tParameterGroup.Count: '{0}'",
                        usingTaskElement.ParameterGroup?.Count ?? 0
                    );
                    TestOutput.WriteLine("\tRuntime: '{0}'",
                        project.ExpandString(usingTaskElement.Runtime)
                    );
                    TestOutput.WriteLine("\tTaskFactory: '{0}'",
                        project.ExpandString(usingTaskElement.TaskFactory)
                    );
                }
            }
        }

        /// <summary>
        ///     Get all referenced package versions from the current test project.
        /// </summary>
        [InlineData("Autofac", "4.6.1")]
        [Theory(DisplayName = "Get referenced package version from the current test project ")]
        public async Task GetReferencedPackageVersion(string packageId, string expectedPackageVersion)
        {
            var projectFile = new FileInfo(
                Path.Combine(TestDirectory.FullName, @"..\..\..\LanguageServer.Engine.Tests.csproj")
            );
            Assert.True(projectFile.Exists,
                $"Cannot find project file {projectFile.FullName}"
            );

            Project project = LoadTestProject(projectFile.FullName);
            using (project.ProjectCollection)
            {
                Dictionary<string, SemanticVersion> referencedPackageVersions = await project.GetReferencedPackageVersions();
                Assert.NotNull(referencedPackageVersions);
                Assert.NotEmpty(referencedPackageVersions);

                SemanticVersion referencedPackageVersion;
                Assert.True(referencedPackageVersions.TryGetValue(packageId, out referencedPackageVersion),
                    $"Cannot find referenced version for package '{packageId}'."
                );
                Assert.Equal(expectedPackageVersion, referencedPackageVersion.ToString());
            }
        }

        /// <summary>
        ///     Load a test project.
        /// </summary>
        /// <param name="relativePathSegments">
        ///     The project file's relative path segments.
        /// </param>
        /// <returns>
        ///     The project.
        /// </returns>
        static Project LoadTestProject(params string[] relativePathSegments)
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            return MSBuildHelper.CreateProjectCollection(TestDirectory.FullName).LoadProject(
                Path.Combine(TestDirectory.FullName,
                    "TestProjects",
                    Path.Combine(relativePathSegments)
                )
            );
        }
    }
}
