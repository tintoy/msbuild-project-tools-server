using MSBuildProjectTools.LanguageServer.Documents;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     Tests for <see cref="DocumentHelper"/>
    /// </summary>
    public class DocumentHelperTests
        : TestBase
    {
        public DocumentHelperTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        /// <summary>
        ///     Verify that <see cref="DocumentHelper.GetDocumentKind"/> correctly determines the <see cref="DocumentKind"/> of the specified document file.
        /// </summary>
        /// <param name="documentFilePath">
        ///     The path to the document file.
        /// </param>
        /// <param name="expectedDocumentKind">
        ///     The expected <see cref="DocumentKind"/>.
        /// </param>
        [Theory]
        [InlineData("foo", DocumentKind.Unknown)]
        [InlineData("/foo", DocumentKind.Unknown)]
        [InlineData("foo.txt", DocumentKind.Unknown)]
        [InlineData("/foo.txt", DocumentKind.Unknown)]
        [InlineData("foo.proj", DocumentKind.Project)]
        [InlineData("/foo.proj", DocumentKind.Project)]
        [InlineData("foo.csproj", DocumentKind.Project)]
        [InlineData("/foo.csproj", DocumentKind.Project)]
        [InlineData("foo.props", DocumentKind.Project)]
        [InlineData("/foo.props", DocumentKind.Project)]
        [InlineData("foo.targets", DocumentKind.Project)]
        [InlineData("/foo.targets", DocumentKind.Project)]
        [InlineData("foo.slnx", DocumentKind.Solution)]
        [InlineData("/foo.slnx", DocumentKind.Solution)]
        public void Workspace_GetDocumentType(string documentFilePath, DocumentKind expectedDocumentKind)
        {
            DocumentKind actualDocumentKind = DocumentHelper.GetDocumentKind(documentFilePath);
            Assert.Equal(expectedDocumentKind, actualDocumentKind);
        }
    }
}
