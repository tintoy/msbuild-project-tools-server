using Xunit;

namespace MSBuildProjectTools.LanguageServer.Tests.ExpressionTests
{
    using SemanticModel.MSBuildExpressions;

    /// <summary>
    ///     Tests for parsing of MSBuild evaluation expressions.
    /// </summary>
    public class EvaluationParserTests
    {
        /// <summary>
        ///     Verify that the Evaluation parser can successfully parse the specified input.
        /// </summary>
        /// <param name="input">
        ///     The source text to parse.
        /// </param>
        /// <param name="expectedSymbolName">
        ///     The expected symbol name.
        /// </param>
        [InlineData("$(Foo)",   "Foo")]
        [InlineData("$( Foo )", "Foo")]
        [InlineData("$( Foo)",  "Foo")]
        [InlineData("$(Foo )",  "Foo")]
        [Theory(DisplayName = "Evaluation parser succeeds with symbol ")]
        public void Parse_Symbol_Success(string input, string expectedSymbolName)
        {
            AssertParser.SucceedsWith(Parsers.Evaluation, input, actualEvaluation =>
            {
                var child = Assert.Single(actualEvaluation.Children);

                Symbol actualSymbol = Assert.IsType<Symbol>(child);
                Assert.Equal(expectedSymbolName, actualSymbol.Name);
            });
        }

        /// <summary>
        ///     Verify that the Evaluation parser can successfully parse the specified input.
        /// </summary>
        /// <param name="input">
        ///     The source text to parse.
        /// </param>
        /// <param name="expectedFunctionName">
        ///     The expected function name.
        /// </param>
        [InlineData("$( Foo() )",                   "Foo")]
        [InlineData("$( Foo('Bar') )",              "Foo")]
        [InlineData("$( Foo('Bar', 'Bonk') )",      "Foo")]
        [InlineData("$(Foo.Bar())",                 "Bar")]
        [InlineData("$(Foo.Bar('Bonk', 'Diddly'))", "Bar")]
        [InlineData("$(Foo.Bar('Baz'))",            "Bar")]
        [InlineData("$([Foo.Bar]::Baz('Bonk'))",    "Baz")]
        [Theory(DisplayName = "Evaluation parser succeeds with function-call ")]
        public void Parse_FunctionCall_Success(string input, string expectedFunctionName)
        {
            AssertParser.SucceedsWith(Parsers.Evaluation, input, actualEvaluation =>
            {
                var child = Assert.Single(actualEvaluation.Children);

                FunctionCall actualFunctionCall = Assert.IsType<FunctionCall>(child);
                Assert.Equal(expectedFunctionName, actualFunctionCall.Name);
            });
        }

        /// <summary>
        ///     Verify that the EvaluationExpression parser cannot successfully parse the specified input.
        /// </summary>
        /// <param name="input">
        ///     The source text to parse.
        /// </param>
        [InlineData("$(1Foo)")]
        [InlineData("$(Foo.Bar)")]
        [Theory(DisplayName = "Evaluation parser fails ")]
        public void Parse_Symbol_Failure(string input)
        {
            AssertParser.Fails(Parsers.Evaluation, input);
        }
    }
}
