using Microsoft.Language.Xml;
using System;
using System.IO;
using Xunit;

#pragma warning disable xUnit2013 // Do not use equality check to check for collection size.

namespace MSBuildProjectTools.LanguageServer.Tests.ExpressionTests
{
    using SemanticModel;
    using SemanticModel.MSBuildExpressions;
    using Utilities;

    /// <summary>
    ///     Tests for <see cref="XmlLocator"/>'s IsExpression and friends.
    /// </summary>
    public class LocatorExpressionTests
    {
        /// <summary>
        ///     The directory for test files.
        /// </summary>
        private static readonly DirectoryInfo TestDirectory = new DirectoryInfo(Path.GetDirectoryName(
            typeof(XmlLocatorTests).Assembly.Location
        ));

        /// <summary>
        ///     Verify that the target line and column are on an expression.
        /// </summary>
        /// <param name="testFileName">
        ///     The name of the test file, without the extension.
        /// </param>
        /// <param name="line">
        ///     The target line.
        /// </param>
        /// <param name="column">
        ///     The target column.
        /// </param>
        /// <param name="expectedExpressionKind">
        ///     The expected kind of expression.
        /// </param>
        [Theory(DisplayName = "On expression ")]
        [InlineData("Test4", 3, 25, ExpressionKind.Evaluate)]
        [InlineData("Test4", 4, 25, ExpressionKind.ItemMetadata)]
        [InlineData("Test4", 5, 30, ExpressionKind.Symbol)]
        [InlineData("Test4", 5, 46, ExpressionKind.ItemMetadata)]
        public void IsExpression_Success(string testFileName, int line, int column, ExpressionKind expectedExpressionKind)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation location = locator.Inspect(testPosition);
            Assert.NotNull(location);

            Assert.True(
                location.IsExpression(out ExpressionNode actualExpression, out Range actualExpressionRange),
                "IsExpression"
            );
            Assert.NotNull(actualExpression);
            
            Assert.Equal(expectedExpressionKind, actualExpression.Kind);
        }

        /// <summary>
        ///     Load a test file.
        /// </summary>
        /// <param name="relativePathSegments">
        ///     The file's relative path segments.
        /// </param>
        /// <returns>
        ///     The file content, as a string.
        /// </returns>
        private static string LoadTestFile(params string[] relativePathSegments)
        {
            if (relativePathSegments == null)
                throw new ArgumentNullException(nameof(relativePathSegments));
            
            return File.ReadAllText(
                Path.Combine(
                    TestDirectory.FullName,
                    Path.Combine(relativePathSegments)
                )
            );
        }
    }
}

#pragma warning restore xUnit2013 // Do not use equality check to check for collection size.
