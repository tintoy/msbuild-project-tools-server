using Superpower;
using Superpower.Parsers;
using System.Collections.Generic;
using System.Globalization;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    /// <summary>
    ///     Token parsers for MSBuild expression syntax.
    /// </summary>
    static class Tokens
    {
        /// <summary>
        ///     Parse a period, ".".
        /// </summary>
        public static TextParser<char> Period = Character.EqualTo('.').Named("token: period");

        /// <summary>
        ///     Parse a period, ",".
        /// </summary>
        public static TextParser<char> Comma = Character.EqualTo(',').Named("token: comma");

        /// <summary>
        ///     Parse a dollar sign, "$".
        /// </summary>
        public static TextParser<char> Dollar = Character.EqualTo('$').Named("token: dollar");

        /// <summary>
        ///     Parse an at sign, "@".
        /// </summary>
        public static TextParser<char> At = Character.EqualTo('@').Named("token: at");

        /// <summary>
        ///     Parse a percentage sign, "%".
        /// </summary>
        public static TextParser<char> Percent = Character.EqualTo('%').Named("token: percent");

        /// <summary>
        ///     Parse a colon, ":".
        /// </summary>
        public static TextParser<char> Colon = Character.EqualTo(':').Named("token: colon");

        /// <summary>
        ///     Parse a semicolon, ";".
        /// </summary>
        public static TextParser<char> Semicolon = Character.EqualTo(';').Named("token: semicolon");

        /// <summary>
        ///     Parse a left parenthesis, "(".
        /// </summary>
        public static TextParser<char> LParen = Character.EqualTo('(').Named("token: left parenthesis");

        /// <summary>
        ///     Parse a right parenthesis, ")".
        /// </summary>
        public static TextParser<char> RParen = Character.EqualTo(')').Named("token: right parenthesis");

        /// <summary>
        ///     Parse a left bracket, "[".
        /// </summary>
        public static TextParser<char> LBracket = Character.EqualTo('[').Named("token: left bracket");

        /// <summary>
        ///     Parse a right bracket, "]".
        /// </summary>
        public static TextParser<char> RBracket = Character.EqualTo(']').Named("token: right bracket");

        /// <summary>
        ///     Parse the opening of an evaluation expression, "$(".
        /// </summary>
        public static TextParser<IEnumerable<char>> EvalOpen = Span.EqualTo("$(").Text().Cast().As<IEnumerable<char>>().Named("token: eval open");

        /// <summary>
        ///     Parse the close of an evaluation expression, ")".
        /// </summary>
        public static TextParser<char> EvalClose = RParen.Named("token: eval close");

        /// <summary>
        ///     Parse the opening of an item group expression, "@(".
        /// </summary>
        public static TextParser<IEnumerable<char>> ItemGroupOpen = Span.EqualTo("@(").Text().Cast().As<IEnumerable<char>>().Named("token: item group open");

        /// <summary>
        ///     Parse the close of an item group expression, ")".
        /// </summary>
        public static TextParser<char> ItemGroupClose = RParen.Named("token: item group close");

        /// <summary>
        ///     Parse the operator in an item group transform, "->".
        /// </summary>
        public static TextParser<IEnumerable<char>> ItemGroupTransformOperator = Span.EqualTo("->").Text().Cast().As<IEnumerable<char>>().Named("token: item group transform operator");

        /// <summary>
        ///     Parse the opening of an item metadata expression, "%(".
        /// </summary>
        public static TextParser<IEnumerable<char>> ItemMetadataOpen = Span.EqualTo("%(").Text().Cast().As<IEnumerable<char>>().Named("token: item metadata open");

        /// <summary>
        ///     Parse the close of an item metadata expression, ")".
        /// </summary>
        public static TextParser<char> ItemMetadataClose = RParen.Named("token: item metadata close");

        /// <summary>
        ///     Parse a logical-AND operator, "And".
        /// </summary>
        public static TextParser<string> AndOperator = Span.EqualTo("And").Text().Named("token: logical-AND operator");

        /// <summary>
        ///     Parse a logical-OR operator, "Or".
        /// </summary>
        public static TextParser<string> OrOperator = Span.EqualTo("Or").Text().Named("token: logical-OR operator");

        /// <summary>
        ///     Parse a logical-NOT operator, "!".
        /// </summary>
        public static TextParser<string> NotOperator = Span.EqualTo("!").Text().Named("token: logical-NOT operator");

        /// <summary>
        ///     Parse an equality operator, "==".
        /// </summary>
        public static TextParser<string> EqualityOperator = Span.EqualTo("==").Text().Named("token: equality operator");

        /// <summary>
        ///     Parse an inequality operator, "!=".
        /// </summary>
        public static TextParser<string> InequalityOperator = Span.EqualTo("!=").Text().Named("token: inequality operator");

        /// <summary>
        ///     Parse a single quote, "'".
        /// </summary>
        public static TextParser<char> SingleQuote = Character.EqualTo('\'').Named("token: single quote");

        /// <summary>
        ///     Parse a single hexadecimal digit, "[0-9A-F]".
        /// </summary>
        public static TextParser<char> HexDigit = Character.Matching(
            predicate: character =>
                (character >= '0' && character <= '9')
                ||
                (character >= 'A' && character <= 'F'),

            name: "token: hexadecimal digit"
        );

        /// <summary>
        ///     Parse an escaped character, "%xx" (where "x" is a hexadecimal digit, and the resulting number represents the ASCII character code).
        /// </summary>
        public static TextParser<char> EscapedChar = Combinators.Named(
            from escape in Character.EqualTo('%')
            from hexDigits in HexDigit.Repeat(2).Text()
            select (char)byte.Parse(hexDigits, NumberStyles.HexNumber),

            name: "token: escaped character"
        );

        /// <summary>
        ///     Parse any character valid within a single-quoted string.
        /// </summary>
        public static TextParser<char> SingleQuotedStringChar =
            EscapedChar.Or(
                Character.AnyChar.Except(
                    SingleQuote.Or(Dollar).Or(Percent).Or(At) // FIXME: Technically these should be EvalOpen and ItemGroupOpen; a single "$" or "@" is legal.
                )
            ).Named("token: single-quoted string character");

        /// <summary>
        ///     Parse a quoted string.
        /// </summary>
        public static TextParser<IEnumerable<char>> QuotedString = Combinators.Named(
            (from leftQuote in SingleQuote
            from stringContents in SingleQuotedStringChar.Many()
            from rightQuote in SingleQuote
            select stringContents).Cast().As<IEnumerable<char>>(),

            name: "token: quoted string"
        );

        /// <summary>
        ///     Parse any character valid within a semicolon-delimited list.
        /// </summary>
        public static TextParser<char> ListChar = Character.AnyChar.Except(Semicolon).Named("token: list character");

        /// <summary>
        ///     Parse a list of strings delimited by semicolons, "ABC;DEF", as character sequences.
        /// </summary>
        public static readonly TextParser<IEnumerable<IEnumerable<char>>> DelimitedList = ListChar.Many().AtLeastOnceDelimitedBy(Semicolon).Named("token: delimited list").Cast().As<IEnumerable<IEnumerable<char>>>();

        /// <summary>
        ///     Parse a list of strings delimited by semicolons, "ABC;DEF", as strings.
        /// </summary>
        public static readonly TextParser<IEnumerable<IEnumerable<char>>> DelimitedListOfStrings = ListChar.Many().Text().AtLeastOnceDelimitedBy(Semicolon).Named("token: delimited string list").Cast().As<IEnumerable<IEnumerable<char>>>();

        /// <summary>
        ///     Parse an identifier, "ABC".
        /// </summary>
        public static TextParser<string> Identifier = Combinators.Named(
            from first in Character.Letter
            from rest in Character.LetterOrDigit.Many().Text()
            select first + rest,

            name: "token: identifier"
        );

        /// <summary>
        ///     Parse a qualified identifier, "ABC.DEF".
        /// </summary>
        public static TextParser<IEnumerable<string>> QualifiedIdentifier = Identifier.AtLeastOnceDelimitedBy(Period).Named("token: qualified identifier").Cast().As<IEnumerable<string>>();
    }
}
