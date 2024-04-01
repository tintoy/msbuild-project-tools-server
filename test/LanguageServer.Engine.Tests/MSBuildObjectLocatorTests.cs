using Microsoft.Build.Evaluation;
using MSBuildProjectTools.LanguageServer.SemanticModel;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     Tests for locating MSBuild objects by position.
    /// </summary>
    /// <param name="testOutput">
    ///     The xUnit test output for the current test.
    /// </param>
    [Collection(MSBuildEngineFixture.CollectionName)]
    public class MSBuildObjectLocatorTests(ITestOutputHelper testOutput)
        : TestBase(testOutput), IDisposable
    {
        /// <summary>
        ///     The project collection for any projects loaded by the current test.
        /// </summary>
        ProjectCollection _projectCollection;

        /// <summary>
        ///     Dispose of resources being used by the test.
        /// </summary>
        public void Dispose()
        {
            if (_projectCollection != null)
            {
                _projectCollection.Dispose();
                _projectCollection = null;
            }
        }

        /// <summary>
        ///     Verify that the <see cref="MSBuildObjectLocator"/> correctly handles a property that is defined, and then redefined, in the same project file.
        /// </summary>
        [Fact]
        public void Can_Locate_Property_Redefined_SameFile()
        {
            TestProject testProject = LoadTestProject("TestProjects", "RedefineProperty.SameFile.csproj");

            var firstElementPosition = new Position(4, 5);                          // <Property2>false</Property2>
            var secondElementPosition = firstElementPosition.Move(lineCount: 1);    // <Property2 Condition=" '$(Property1)' == 'true' ">true</Property2>


            // First "Property2" element (the overridden one).
            //      <Property2>false</Property2>
            XmlLocation firstLocation = testProject.XmlLocations.Inspect(firstElementPosition);
            
            XSElement firstPropertyElement;
            Assert.True(firstLocation.IsElement(out firstPropertyElement));
            Assert.Equal("Property2", firstPropertyElement.Name);

            // Second "Property2" element (the overriding one).
            //      <Property2 Condition=" '$(Property1)' == 'true' ">true</Property2>
            XmlLocation secondLocation = testProject.XmlLocations.Inspect(secondElementPosition);
            
            XSElement secondPropertyElement;
            Assert.True(secondLocation.IsElement(out secondPropertyElement));
            Assert.Equal("Property2", secondPropertyElement.Name);

            // The MSBuild property "Property2" corresponding to the first "Property" element.
            MSBuildObject firstMSBuildObject = testProject.ObjectLocations.Find(firstElementPosition);
            MSBuildProperty propertyFromFirstPosition = Assert.IsAssignableFrom<MSBuildProperty>(firstMSBuildObject);
            Assert.Equal("Property2", propertyFromFirstPosition.Name);
            Assert.Equal("true", propertyFromFirstPosition.Value); // property has second, overridden value
            Assert.Equal(propertyFromFirstPosition.Element.Range, firstPropertyElement.Range); // i.e. property comes from the second Property2 element, not the first.

            // The MSBuild property "Property2" corresponding to the second "Property" element.
            MSBuildObject secondMSBuildObject = testProject.ObjectLocations.Find(secondElementPosition);
            MSBuildProperty propertyFromSecondPosition = Assert.IsAssignableFrom<MSBuildProperty>(secondMSBuildObject);
            Assert.Equal("Property2", propertyFromSecondPosition.Name);
            Assert.Equal("true", propertyFromSecondPosition.Value); // property has second, overridden value
            Assert.Equal(propertyFromSecondPosition.Element.Range, secondPropertyElement.Range); // i.e. property comes from the second Property2 element, not the first.
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
        TestProject LoadTestProject(params string[] relativePathSegments)
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            if (_projectCollection == null)
                _projectCollection = TestProjects.CreateProjectCollection<MSBuildObjectLocatorTests>(Log, relativePathSegments);

            return _projectCollection.LoadTestProject<MSBuildObjectLocatorTests>(relativePathSegments);
        }
    }
}
