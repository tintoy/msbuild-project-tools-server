using Sprache;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    /// <summary>
    ///     Represents an MSBuild quoted-string literal expression.
    /// </summary>
    /// <remarks>
    ///     Quoted strings can contain sub-expressions, but quoted string literals cannot.
    /// </remarks>
    public sealed class QuotedStringLiteral
        : QuotedString, IPositionAware<QuotedStringLiteral>
    {
        /// <summary>
        ///     The node kind.
        /// </summary>
        public override ExpressionKind Kind => ExpressionKind.QuotedString;

        /// <summary>
        ///     The string content.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        ///     The quoted string's textual content.
        /// </summary>
        public override string StringContent => Content;

        /// <summary>
        ///     Get a string representation of the expression node.
        /// </summary>
        /// <returns>
        ///     The string representation.
        /// </returns>
        public override string ToString() => $"MSBuild QuotedStringLiteral @ {Range}";

        /// <summary>
        ///     Update positioning information.
        /// </summary>
        /// <param name="startPosition">
        ///     The node's starting position.
        /// </param>
        /// <param name="length">
        ///     The node length.
        /// </param>
        /// <returns>
        ///     The <see cref="ExpressionNode"/>.
        /// </returns>
        QuotedStringLiteral IPositionAware<QuotedStringLiteral>.SetPos(Sprache.Position startPosition, int length)
        {
            SetPosition(startPosition, length);

            return this;
        }
    }
}
