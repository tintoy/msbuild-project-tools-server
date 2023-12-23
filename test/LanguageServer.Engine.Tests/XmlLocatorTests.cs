using Microsoft.Language.Xml;
using System;
using System.IO;
using Xunit;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Tests for locating XML by position.
    /// </summary>
    public class XmlLocatorTests
    {
        /// <summary>
        ///     The directory for test files.
        /// </summary>
        static readonly DirectoryInfo TestDirectory = new DirectoryInfo(Path.GetDirectoryName(
            typeof(XmlLocatorTests).Assembly.Location
        ));

        /// <summary>
        ///     Verify that the target line and column lie on a <see cref="SyntaxList"/> inside element 1.
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
        [InlineData("Test1", 2, 5)]
        [InlineData("Test1", 3, 9)]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "Inside element ")]
        public void InsideElement1(string testFileName, int line, int column)
        {
            // TODO: Change this test to use XmlLocator.

            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax xmlDocument = Parser.ParseText(testXml);

            int absolutePosition = positions.GetAbsolutePosition(testPosition) - 1; // To find out if we can insert an element, make sure we find the node at the position ONE BEFORE the insertion point!
            SyntaxNode foundNode = xmlDocument.FindNode(absolutePosition,
                descendIntoChildren: node => true
            );
            Assert.NotNull(foundNode);
            Assert.IsAssignableFrom<SyntaxList>(foundNode);
            SyntaxList list = (SyntaxList)foundNode;

            Range listSpan = list.Span.ToNative(positions);
            Assert.True(
                listSpan.Contains(testPosition),
                "List's span must contain the test position."
            );
        }

        /// <summary>
        ///     Verify that the target line and column lie after within an element's content.
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
        /// <param name="expectedNodeKind">
        ///     The kind of node expected at the position.
        /// </param>
        [InlineData("Test1", 3, 21, XSNodeKind.Whitespace)]
        [InlineData("Test1", 3, 22, XSNodeKind.Whitespace)]
        [InlineData("Test2", 11, 8, XSNodeKind.Whitespace)]
        [InlineData("Test2", 5, 22, XSNodeKind.Text)]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "Within element content ")]
        public void InElementContent(string testFileName, int line, int column, XSNodeKind expectedNodeKind)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation result = locator.Inspect(testPosition);

            Assert.NotNull(result);
            Assert.Equal(expectedNodeKind, result.Node.Kind);
            Assert.True(result.IsElementContent(), "IsElementContent");

            // TODO: Verify Parent, PreviousSibling, and NextSibling.
        }

        /// <summary>
        ///     Verify that the target line and column lie within an empty element's name.
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
        /// <param name="expectedElementName">
        ///     The expected element name.
        /// </param>
        [InlineData("Test2", 11, 10, "PackageReference")]
        [InlineData("Test2", 12, 18, "PackageReference")]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "Within empty element's name ")]
        public void InEmptyElementName(string testFileName, int line, int column, string expectedElementName)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation result = locator.Inspect(testPosition);

            Assert.NotNull(result);
            Assert.Equal(XSNodeKind.Element, result.Node.Kind);
            Assert.True(result.IsElement(), "IsElement");

            XSElement element = (XSElement)result.Node;
            Assert.Equal(expectedElementName, element.Name);

            Assert.True(result.IsEmptyElement(), "IsEmptyElement");
            Assert.True(result.IsName(), "IsName");

            Assert.False(result.IsElementContent(), "IsElementContent");

            // TODO: Verify Parent, PreviousSibling, and NextSibling.
        }

        /// <summary>
        ///     Verify that the target line and column lie within an attribute's value (excluding the enclosing quotes).
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
        /// <param name="expectedAttributeName">
        ///     The expected attribute name.
        /// </param>
        [InlineData("Test2", 11, 36, "Include")]
        [InlineData("Test2", 11, 37, "Include")]
        [InlineData("Test2", 11, 51, "Include")]
        [InlineData("Test2", 11, 62, "Version")]
        [InlineData("Test2", 11, 63, "Version")]
        [InlineData("Test2", 11, 68, "Version")]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "Within attribute's value ")]
        public void InAttributeValue(string testFileName, int line, int column, string expectedAttributeName)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation result = locator.Inspect(testPosition);
            Assert.NotNull(result);

            Assert.True(result.IsAttribute(out XSAttribute attribute), "IsAttribute");
            Assert.True(result.IsAttributeValue(), "IsAttributeValue");

            Assert.Equal(expectedAttributeName, attribute.Name);

            // TODO: Verify Parent, PreviousSibling, and NextSibling.
        }

        /// <summary>
        ///     Verify that the target line and column are on an element that can be replaced by completion.
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
        [InlineData("Invalid1.DoubleOpeningTag", 4, 10)]
        [InlineData("Invalid1.EmptyOpeningTag", 5, 10)]
        [InlineData("Invalid2.DoubleOpeningTag", 13, 10)]
        [InlineData("Invalid2.EmptyOpeningTag", 13, 65)]
        [InlineData("Invalid2.NoClosingTag", 14, 10)]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "On element that can be replaced by completion ")]
        public void CanCompleteElement(string testFileName, int line, int column)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation location = locator.Inspect(testPosition);
            Assert.NotNull(location);

            Assert.True(location.CanCompleteElement(out XSElement replacingElement), "CanCompleteElement");
            Assert.NotNull(replacingElement);
        }

        /// <summary>
        ///     Verify that the target line and column are on an element that can be replaced by completion.
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
        [InlineData("Invalid1.DoubleOpeningTag", 4, 10, "Element2")]
        [InlineData("Invalid1.EmptyOpeningTag", 5, 10, "Element2")]
        [InlineData("Invalid2.DoubleOpeningTag", 13, 10, "ItemGroup")]
        [InlineData("Invalid2.EmptyOpeningTag", 13, 65, "ItemGroup")]
        [InlineData("Invalid2.NoClosingTag", 10, 5, "Project")]
        [InlineData("Invalid2.EmptyOpeningTag.ChildOfRoot", 2, 6, "Project")]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "On completable element if parent name matches ")]
        public void CanCompleteElementInParentNamed(string testFileName, int line, int column, string expectedParent)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation location = locator.Inspect(testPosition);
            Assert.NotNull(location);

            XSPath expectedParentPath = XSPath.Parse(expectedParent);

            Assert.True(
                location.CanCompleteElement(out XSElement replaceElement, parentPath: expectedParentPath),
                "CanCompleteElement"
            );
            Assert.NotNull(replaceElement);
            Assert.Equal(expectedParent, replaceElement.ParentElement?.Name);
        }

        /// <summary>
        ///     Verify that the target line and column are on an element that can be replaced by completion.
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
        /// <param name="expectedParent">
        ///     The name of the expected parent element.
        /// </param>
        [InlineData("Invalid1.DoubleOpeningTag", 4, 10, "Element2")]
        [InlineData("Invalid1.EmptyOpeningTag", 5, 10, "Element2")]
        [InlineData("Invalid2.DoubleOpeningTag", 13, 10, "ItemGroup")]
        [InlineData("Invalid2.EmptyOpeningTag", 13, 65, "ItemGroup")]
        [InlineData("Invalid2.NoClosingTag", 10, 5, "Project")]
        [InlineData("Invalid2.EmptyOpeningTag.ChildOfRoot", 2, 6, "Project")]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "On completable element if parent relative path matches ")]
        public void CanCompleteElementInParentWithRelativePath(string testFileName, int line, int column, string expectedParent)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation location = locator.Inspect(testPosition);
            Assert.NotNull(location);

            XSPath expectedParentPath = XSPath.Parse(expectedParent);

            Assert.True(
                location.CanCompleteElement(out XSElement replaceElement, parentPath: expectedParentPath),
                "CanCompleteElement"
            );
            Assert.NotNull(replaceElement);
            Assert.Equal(expectedParent, replaceElement.ParentElement?.Name);
        }

        /// <summary>
        ///     Verify that the target line and column refer to a location where an element can be inserted by completion.
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
        [InlineData("SimpleProject", 7, 36, "PropertyGroup")]
        [InlineData("SimpleProject", 8, 21, "Project")]
        [InlineData("SimpleProject.InsertElement.PropertyGroup", 7, 37, "PropertyGroup")]
        [InlineData("SimpleProject.InsertElement.Project", 8, 22, "Project")]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "On completable element if parent relative path matches ")]
        public void CanInsertElementInParentWithRelativePath(string testFileName, int line, int column, string expectedParent)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation location = locator.Inspect(testPosition);
            Assert.NotNull(location);

            XSPath expectedParentPath = XSPath.Parse(expectedParent);

            Assert.True(
                location.CanCompleteElement(out XSElement replaceElement, parentPath: expectedParentPath),
                "CanCompleteElement"
            );
            Assert.Null(replaceElement);

            Assert.Equal(expectedParent, location?.Parent?.Name);
        }

        /// <summary>
        ///     Verify that the target line and column are on an element where an attribute can be created by completion.
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
        /// <param name="expectedElementName">
        ///     The name of the element to which the attribute will be added.
        /// </param>
        [InlineData("Test1", 6, 14, "Element5", PaddingType.Leading)]
        [InlineData("Test2", 17, 18, "Compile", PaddingType.Trailing)]
        [InlineData("Test2", 17, 36, "Compile", PaddingType.Leading)]
        [InlineData("Test2", 17, 37, "Compile", PaddingType.Trailing)]
        [InlineData("Test2", 17, 53, "Compile", PaddingType.Leading)]
        [InlineData("Test2", 17, 54, "Compile", PaddingType.None)]
        [InlineData("Test3", 2, 15, "Element2", PaddingType.None)]
        [Trait("Component", "XmlLocator")]
        [Theory(DisplayName = "On completable attribute where element name matches ")]
        public void CanCompleteAttribute(string testFileName, int line, int column, string expectedElementName, PaddingType expectedPadding)
        {
            Position testPosition = new Position(line, column);

            string testXml = LoadTestFile("TestFiles", testFileName + ".xml");
            TextPositions positions = new TextPositions(testXml);
            XmlDocumentSyntax document = Parser.ParseText(testXml);

            XmlLocator locator = new XmlLocator(document, positions);
            XmlLocation location = locator.Inspect(testPosition);
            Assert.NotNull(location);

            XSPath elementPath = XSPath.Parse(expectedElementName);

            Assert.True(
                location.CanCompleteAttribute(out XSElement element, out XSAttribute replaceAttribute, out PaddingType needsPadding, onElementWithPath: elementPath),
                "CanCompleteAttribute"
            );
            Assert.NotNull(element);
            Assert.Null(replaceAttribute);
            Assert.Equal(expectedPadding, needsPadding);
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
        static string LoadTestFile(params string[] relativePathSegments)
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
