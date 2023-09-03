using Sprache;
using System.Collections.Generic;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    /// <summary>
    ///     Represents an MSBuild quoted-string expression.
    /// </summary>
    public class QuotedString
        : ExpressionContainerNode, IPositionAware<QuotedString>
    {
        /// <summary>
        ///     The node kind.
        /// </summary>
        public override ExpressionKind Kind => ExpressionKind.QuotedString;

        /// <summary>
        ///     Quoted strings are never virtual.
        /// </summary>
        public override bool IsVirtual => false;

        /// <summary>
        ///     Evaluation expressions (if any) contained in the string.
        /// </summary>
        public IEnumerable<Evaluate> Evaluations => Children.OfType<Evaluate>();

        /// <summary>
        ///     The quoted string's textual content (without evaluation expressions).
        /// </summary>
        public virtual string StringContent => string.Join("",
            Children.OfType<StringContent>().Select(
                stringContent => stringContent.Content
            )
        );

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
        QuotedString IPositionAware<QuotedString>.SetPos(Sprache.Position startPosition, int length)
        {
            SetPosition(startPosition, length);

            return this;
        }
    }
}
