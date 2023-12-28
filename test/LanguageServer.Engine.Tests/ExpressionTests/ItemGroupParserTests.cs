using Xunit;

namespace MSBuildProjectTools.LanguageServer.Tests.ExpressionTests
{
    using SemanticModel.MSBuildExpressions;

    /// <summary>
    ///     Tests for parsing of MSBuild item group expressions.
    /// </summary>
    public class ItemGroupParserTests
    {
        /// <summary>
        ///     Verify that the ItemGroup parser can successfully parse the specified input.
        /// </summary>
        /// <param name="input">
        ///     The source text to parse.
        /// </param>
        /// <param name="expectedItemGroupName">
        ///     The expected symbol name.
        /// </param>
        [InlineData("@()",      ""   )]
        [InlineData("@(Foo)",   "Foo")]
        [InlineData("@( Foo )", "Foo")]
        [InlineData("@( Foo)",  "Foo")]
        [InlineData("@(Foo )",  "Foo")]
        [Theory(DisplayName = "ItemGroup parser succeeds ")]
        public void Parse_Success(string input, string expectedItemGroupName)
        {
            AssertParser.SucceedsWith(Parsers.ItemGroup, input, actualItemGroup =>
            {
                Assert.Equal(expectedItemGroupName, actualItemGroup.Name);
            });
        }

        /// <summary>
        ///     Verify that the ItemGroup parser cannot successfully parse the specified input.
        /// </summary>
        /// <param name="input">
        ///     The source text to parse.
        /// </param>
        [InlineData("@(1Foo)"   )]
        [InlineData("@(Foo.Bar)")]
        [Theory(DisplayName = "ItemGroup parser fails ")]
        public void Parse_Failure(string input)
        {
            AssertParser.Fails(Parsers.ItemGroup, input);
        }
    }
}
