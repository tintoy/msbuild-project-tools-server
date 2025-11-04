using Superpower;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    using Position = Superpower.Model.Position;

    /// <summary>
    ///     Represents a list item separator with leading and trailing whitespace.
    /// </summary>
    public sealed class ListSeparator
        : ExpressionNode, IPositionAware<ListSeparator>
    {
        /// <summary>
        ///     Create a new <see cref="ListSeparator"/>.
        /// </summary>
        public ListSeparator()
        {
        }

        /// <summary>
        ///     The node kind.
        /// </summary>
        public override ExpressionKind Kind => ExpressionKind.SimpleListSeparator;

        /// <summary>
        ///     The offset, in characters, of the actual separator character from the <see cref="ExpressionNode.AbsoluteStart"/> of the <see cref="ListSeparator"/>.
        /// </summary>
        public int SeparatorOffset { get; internal set; }

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
        ListSeparator IPositionAware<ListSeparator>.SetPos(Position startPosition, int length)
        {
            SetPosition(startPosition, length);

            return this;
        }
    }
}
