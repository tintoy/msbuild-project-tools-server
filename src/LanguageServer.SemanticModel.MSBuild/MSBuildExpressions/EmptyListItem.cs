using Superpower;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    using Position = Superpower.Model.Position;

    /// <summary>
    ///     Represents an empty MSBuild list item.
    /// </summary>
    public sealed class EmptyListItem
        : ExpressionNode, IPositionAware<EmptyListItem>
    {
        /// <summary>
        ///     Create a new <see cref="EmptyListItem"/>.
        /// </summary>
        public EmptyListItem()
        {
        }

        /// <summary>
        ///     The node kind.
        /// </summary>
        public override ExpressionKind Kind => ExpressionKind.EmptyListItem;

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
        EmptyListItem IPositionAware<EmptyListItem>.SetPos(Position startPosition, int length)
        {
            SetPosition(startPosition, length);

            return this;
        }
    }
}
