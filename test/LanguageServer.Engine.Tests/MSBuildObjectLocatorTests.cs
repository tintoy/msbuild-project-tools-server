using Microsoft.Build.Evaluation;
using Microsoft.Language.Xml;
using MSBuildProjectTools.LanguageServer.SemanticModel;
using MSBuildProjectTools.LanguageServer.Utilities;
using Serilog;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     Tests for locating MSBuild objects by position.
    /// </summary>
    [Collection(MSBuildEngineFixture.CollectionName)]
    public class MSBuildObjectLocatorTests
        : TestBase, IDisposable
    {
        /// <summary>
        ///     The directory for test files.
        /// </summary>
        static readonly DirectoryInfo TestDirectory = new DirectoryInfo(Path.GetDirectoryName(
            typeof(MSBuildObjectLocatorTests).Assembly.Location
        ));

        ProjectCollection _projectCollection;

        public MSBuildObjectLocatorTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        public void Dispose()
        {
            if (_projectCollection != null)
            {
                _projectCollection.Dispose();
                _projectCollection = null;
            }
        }

        [Fact]
        public void Can_Locate_Property_Redefined_SameFile()
        {
            TestProject testProject = LoadTestProject("TestProjects", "RedefineProperty.SameFile.csproj");

            var firstElementPosition = new Position(4, 5);                          // <Property2>false</Property2>
            var secondElementPosition = firstElementPosition.Move(lineCount: 1);    // <Property2 Condition=" '$(Property1)' == 'true' ">true</Property2>

            XmlLocation firstLocation = testProject.XmlLocations.Inspect(firstElementPosition);
            MSBuildObject firstMSBuildObject = testProject.ObjectLocations.Find(firstElementPosition);

            XmlLocation secondLocation = testProject.XmlLocations.Inspect(secondElementPosition);
            MSBuildObject secondMSBuildObject = testProject.ObjectLocations.Find(secondElementPosition);

            // Second "Property2" element (the overriding one).
            //      <Property2>false</Property2>
            XSElement secondPropertyElement;
            Assert.True(secondLocation.IsElement(out secondPropertyElement));
            Assert.Equal("Property2", secondPropertyElement.Name);

            MSBuildProperty propertyFromSecondPosition = Assert.IsAssignableFrom<MSBuildProperty>(secondMSBuildObject);
            Assert.Equal("Property2", propertyFromSecondPosition.Name);
            Assert.Equal("true", propertyFromSecondPosition.Value); // property has second, overridden value
            Assert.Equal(propertyFromSecondPosition.Element.Range, secondPropertyElement.Range); // i.e. property comes from the second Property2 element, not the first.

            // First "Property2" element (the overridden one).
            //      <Property2 Condition=" '$(Property1)' == 'true' ">true</Property2>
            XSElement firstPropertyElement;
            Assert.True(firstLocation.IsElement(out firstPropertyElement));
            Assert.Equal("Property2", firstPropertyElement.Name);

            MSBuildProperty propertyFromFirstPosition = Assert.IsAssignableFrom<MSBuildProperty>(firstMSBuildObject);
            Assert.Equal("Property2", propertyFromFirstPosition.Name);
            Assert.Equal("true", propertyFromFirstPosition.Value); // property has second, overridden value
            Assert.Equal(propertyFromFirstPosition.Element.Range, firstPropertyElement.Range); // i.e. property comes from the second Property2 element, not the first.
        }

        /// <summary>
        ///     Load a test project.
        /// </summary>
        /// <param name="relativePathSegments">
        ///     The file's relative path segments.
        /// </param>
        /// <returns>
        ///     The loaded project, as a <see cref="TestProject"/>.
        /// </returns>
        TestProject LoadTestProject(params string[] relativePathSegments) => TestProject.Load(ref _projectCollection, Log, relativePathSegments);

        /// <summary>
        ///     An MSBuild project, loaded for a test.
        /// </summary>
        /// <param name="MSBuildProject">
        ///     The underlying MSBuild <see cref="Project"/>.
        /// </param>
        /// <param name="ObjectLocations">
        ///     The <see cref="MSBuildObjectLocator"/> for the project.
        /// </param>
        /// <param name="XmlLocations">
        ///     The <see cref="XmlLocator"/> for the project XML.
        /// </param>
        /// <param name="TextPositions">
        ///     The <see cref="TextPositions"/> for the project text.
        /// </param>
        record class TestProject(Project MSBuildProject, MSBuildObjectLocator ObjectLocations, XmlLocator XmlLocations, TextPositions TextPositions)
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

                string projectFileName = Path.Combine(
                    TestDirectory.FullName,
                    Path.Combine(relativePathSegments)
                );
                string projectDirectory = Path.GetDirectoryName(projectFileName);

                if (projectCollection == null)
                    projectCollection = MSBuildHelper.CreateProjectCollection(projectDirectory, logger: logger);

                string projectFileContent = File.ReadAllText(projectFileName);
                XmlDocumentSyntax projectXml = Parser.ParseText(projectFileContent);

                var xmlPositions = new TextPositions(projectFileContent);
                var xmlLocator = new XmlLocator(projectXml, xmlPositions);

                Project msbuildProject = projectCollection.LoadProject(projectFileName);
                var msbuildObjectLocator = new MSBuildObjectLocator(msbuildProject, xmlLocator, xmlPositions);

                return new TestProject(msbuildProject, msbuildObjectLocator, xmlLocator, xmlPositions);
            }
        };
    }
}
