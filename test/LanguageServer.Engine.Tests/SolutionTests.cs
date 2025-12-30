using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.Tests
{
    using SemanticModel;

    /// <summary>
    ///     Test suite for <see cref="VsSolution"/>, <see cref="VsSolutionObject"/>, <see cref="VsSolutionObjectLocator"/>, and related types.
    /// </summary>
    /// <param name="testOutput"></param>
    public class SolutionTests(ITestOutputHelper testOutput)
        : TestBase(testOutput)
    {
        /// <summary>
        ///     Verify that <see cref="VsSolutionObjectLocator"/> can determine the type of solution object at a given location.
        /// </summary>
        /// <param name="solutionFileName">
        ///     The name of the target solution file (relative to "./TestSolutions").
        /// </param>
        /// <param name="line">
        ///     The target line number (1-based).
        /// </param>
        /// <param name="column">
        ///     The target column (1-based).
        /// </param>
        /// <param name="expectedKind">
        ///     A <see cref="VsSolutionObjectKind"/> value representing the expected solution object kind at the target location.
        /// </param>
        [Theory(DisplayName = "Can determine the type of solution object at location ")]
        [InlineData("TestSolution1.slnx", 1, 1, VsSolutionObjectKind.Solution)]
        [InlineData("TestSolution1.slnx", 7, 9, VsSolutionObjectKind.Folder)]
        [InlineData("TestSolution1.slnx", 18, 4, VsSolutionObjectKind.Solution)]
        [InlineData("TestSolution1.slnx", 18, 5, VsSolutionObjectKind.Folder)]
        [InlineData("TestSolution1.slnx", 18, 14, VsSolutionObjectKind.Folder)]
        [InlineData("TestSolution1.slnx", 18, 25, VsSolutionObjectKind.Folder)]
        [InlineData("TestSolution1.slnx", 18, 26, VsSolutionObjectKind.Folder)]
        [InlineData("TestSolution1.slnx", 18, 27, VsSolutionObjectKind.Folder)]
        public async Task Can_Determine_ObjectKind_At_Location(string solutionFileName, int line, int column, VsSolutionObjectKind expectedKind)
        {
            TestSolution testData = await LoadTestSolution("TestSolutions", solutionFileName);

            var targetPosition = new Position(line, column);
            VsSolutionObject? objectAtLocation = testData.ObjectLocations.Find(targetPosition);
            Assert.NotNull(objectAtLocation);
            Assert.Equal(expectedKind, objectAtLocation.Kind);
        }

        /// <summary>
        ///     Load a test solution.
        /// </summary>
        /// <param name="relativePathSegments">
        ///     The solution file's relative path segments.
        /// </param>
        /// <returns>
        ///     The test solution.
        /// </returns>
        ValueTask<TestSolution> LoadTestSolution(params string[] relativePathSegments)
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));

            return TestSolutions.LoadTestSolution(GetType(), relativePathSegments);
        }
    }
}
