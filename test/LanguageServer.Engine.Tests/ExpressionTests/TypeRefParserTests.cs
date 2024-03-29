using Xunit;

namespace MSBuildProjectTools.LanguageServer.Tests.ExpressionTests
{
    using SemanticModel.MSBuildExpressions;

    /// <summary>
    ///     Tests for parsing of MSBuild type-reference expressions.
    /// </summary>
    public class TypeRefParserTests
    {
        /// <summary>
        ///     Verify that the TypeRef parser can successfully parse the specified input.
        /// </summary>
        /// <param name="input">
        ///     The source text to parse.
        /// </param>
        /// <param name="expectedTypeName">
        ///     The expected type name.
        /// </param>
        /// <param name="expectedTypeNamespace">
        ///     The expected type namespace.
        /// </param>
        [InlineData("[Foo]",     "Foo", ""   )]
        [InlineData("[Foo.Bar]", "Bar", "Foo")]
        [Theory(DisplayName = "TypeRef parser succeeds with symbol ")]
        public void Parse_Success(string input, string expectedTypeName, string expectedTypeNamespace)
        {
            AssertParser.SucceedsWith(Parsers.TypeRef, input, actualTypeRef =>
            {
                Assert.Equal(expectedTypeName, actualTypeRef.Name);
                Assert.Equal(expectedTypeNamespace, actualTypeRef.Namespace);
            });
        }

        /// <summary>
        ///     Verify that the TypeRefExpression parser cannot successfully parse the specified input.
        /// </summary>
        /// <param name="input">
        ///     The source text to parse.
        /// </param>
        [InlineData("[1Foo]"   )]
        [InlineData("[Foo Bar]")]
        [Theory(DisplayName = "TypeRef parser fails ")]
        public void Parse_Symbol_Failure(string input)
        {
            AssertParser.Fails(Parsers.TypeRef, input);
        }
    }
}
