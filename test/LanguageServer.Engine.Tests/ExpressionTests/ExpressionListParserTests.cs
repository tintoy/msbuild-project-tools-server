using Sprache;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.Tests.ExpressionTests
{
    using SemanticModel.MSBuildExpressions;

    /// <summary>
    ///     Tests for parsing of MSBuild list expressions.
    /// </summary>
    public class ExpressionListParserTests(ITestOutputHelper testOutput)
    {
        /// <summary>
        ///     Output for the current test.
        /// </summary>
        ITestOutputHelper TestOutput { get; } = testOutput;

        /// <summary>
        ///     Verify that the SimpleListItem parser can successfully parse the specified input.
        /// </summary>
        /// <param name="input">
        ///     The source text to parse.
        /// </param>
        [InlineData(""    )]
        [InlineData(" "   )]
        [InlineData("ABC" )]
        [InlineData(" ABC")]
        [InlineData("ABC ")]
        [Theory(DisplayName = "SimpleListItem parser succeeds ")]
        public void ParseSimpleListItem_Success(string input)
        {
            AssertParser.SucceedsWith(Parsers.SimpleLists.Item, input, actualItem =>
            {
                Assert.Equal(input, actualItem.Value);
            });
        }

        /// <summary>
        ///     Generate test actions for the specified generic item values.
        /// </summary>
        /// <param name="expectedValues">
        ///     The values to expect.
        /// </param>
        /// <param name="testActionTemplate">
        ///     A test action action that receives each expected value and its corresponding actual <see cref="ExpressionNode"/>.
        /// </param>
        /// <returns>
        ///     An array of test actions.
        /// </returns>
        static Action<ExpressionNode>[] HasListItems(string[] expectedValues, Action<string, ExpressionNode> testActionTemplate)
        {
            if (expectedValues == null)
                throw new ArgumentNullException(nameof(expectedValues));

            if (testActionTemplate == null)
                throw new ArgumentNullException(nameof(testActionTemplate));

            return
                expectedValues.Select<string, Action<ExpressionNode>>(expectedValue =>
                    actual => testActionTemplate(expectedValue, actual)
                )
                .ToArray();
        }

        /// <summary>
        ///     Dump a simple list to the output for the current test.
        /// </summary>
        /// <param name="list">
        ///     The <see cref="SimpleList"/>.
        /// </param>
        /// <param name="input">
        ///     The original (unparsed) input.
        /// </param>
        void DumpList(SimpleList list, string input)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            TestOutput.WriteLine("Input: '{0}'", input);
            TestOutput.WriteLine(
                new string('=', input.Length + 9)
            );

            foreach (ExpressionNode child in list.Children)
            {
                TestOutput.WriteLine("{0} ({1}..{2})",
                    child.Kind,
                    child.AbsoluteStart,
                    child.AbsoluteEnd
                );
                if (child is SimpleListItem actualItem)
                    TestOutput.WriteLine("\tValue = '{0}'", actualItem.Value);
                else if (child is ListSeparator actualSeparator)
                    TestOutput.WriteLine("\tSeparatorOffset = {0}", actualSeparator.SeparatorOffset);
            }
        }
    }
}
