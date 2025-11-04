using Superpower;
using Superpower.Model;
using System.Collections.Generic;
using Xunit;
using System;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     Assertions for testing parsers.
    /// </summary>
    public static class AssertParser
    {
        /// <summary>
        ///     Assert that the parser successfully parses the specified input.
        /// </summary>
        /// <param name="parser">
        ///     The parser under test.
        /// </param>
        /// <param name="input">
        ///     The test input.
        /// </param>
        public static void Succeeds<T>(TextParser<T> parser, string input)
        {
            SucceedsWith(parser, input, successResult => { });
        }

        /// <summary>
        ///     Assert that a parser succeeds with a single result.
        /// </summary>
        /// <param name="parser">
        ///     The parser under test.
        /// </param>
        /// <param name="input">
        ///     The test input.
        /// </param>
        /// <param name="expectedResult">
        ///     The expected result.
        /// </param>
        public static void SucceedsWithOne<TResult>(TextParser<IEnumerable<TResult>> parser, string input, TResult expectedResult)
        {
            SucceedsWith(parser, input, actualResults =>
            {
                Assert.Collection(actualResults,
                    singleResult => Assert.Equal(expectedResult, singleResult)
                );
            });
        }

        /// <summary>
        ///     Assert that a parser succeeds with 1 or more results.
        /// </summary>
        /// <param name="parser">
        ///     The parser under test.
        /// </param>
        /// <param name="input">
        ///     The test input.
        /// </param>
        /// <param name="expectedResults">
        ///     The expected results.
        /// </param>
        public static void SucceedsWithMany<TResult>(TextParser<IEnumerable<TResult>> parser, string input, IEnumerable<TResult> expectedResults)
        {
            SucceedsWith(parser, input, actualResults =>
            {
                Assert.Equal(expectedResults, actualResults);
            });
        }

        /// <summary>
        ///     Assert that a parser successfully parses all input.
        /// </summary>
        /// <param name="parser">
        ///     The parser under test.
        /// </param>
        /// <param name="input">
        ///     The test input.
        /// </param>
        public static void SucceedsWithAll(TextParser<IEnumerable<char>> parser, string input)
        {
            SucceedsWithMany(parser, input, input.ToCharArray());
        }

        /// <summary>
        ///     Assert that a parser succeeds with the specified result.
        /// </summary>
        /// <param name="parser">
        ///     The parser under test.
        /// </param>
        /// <param name="input">
        ///     The test input.
        /// </param>
        /// <param name="resultAssertion">
        ///     An action that makes assertions about the result.
        /// </param>
        public static void SucceedsWith<TResult>(TextParser<TResult> parser, string input, Action<TResult> resultAssertion)
        {
            Result<TResult> result = parser.TryParse(input);

            string expectations = result.Expectations != null ? string.Join(", ", result.Expectations.Select(
                expectation => string.Format("'{0}'", expectation)
            )) : string.Empty;

            Assert.True(result.HasValue, $"Parsing of '{input}' failed unexpectedly (expected [{expectations}] at {result.Remainder}).");

            resultAssertion(result.Value);
        }

        /// <summary>
        ///     Assert that the parser fails to parse the specified input.
        /// </summary>
        /// <param name="parser">
        ///     The parser under test.
        /// </param>
        /// <param name="input">
        ///     The test input.
        /// </param>
        public static void Fails<T>(TextParser<T> parser, string input)
        {
            FailsWith(parser, input, failureResult => { });
        }

        /// <summary>
        ///     Assert that the parser fails to parse the specified input.
        /// </summary>
        /// <param name="parser">
        ///     The parser under test.
        /// </param>
        /// <param name="input">
        ///     The test input.
        /// </param>
        /// <param name="position">
        ///     The position at which parsing is expected to fail.
        /// </param>
        public static void FailsAt<T>(TextParser<T> parser, string input, int position)
        {
            FailsWith(parser, input, failureResult =>
            {
                Assert.Equal(position, failureResult.Remainder.Position.Absolute);
            });
        }

        /// <summary>
        ///     Assert that a parser fails with the specified result.
        /// </summary>
        /// <param name="parser">
        ///     The parser under test.
        /// </param>
        /// <param name="input">
        ///     The test input.
        /// </param>
        /// <param name="resultAssertion">
        ///     An action that makes assertions about the result.
        /// </param>
        public static void FailsWith<T>(TextParser<T> parser, string input, Action<Result<T>> resultAssertion)
        {
            Result<T> result = parser.TryParse(input);
            Assert.False(result.HasValue, $"Parsing of '{input}' succeeded unexpectedly.");

            resultAssertion(result);
        }
    }
}
