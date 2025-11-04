using Superpower;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    using Position = Superpower.Model.Position;

    /// <summary>
    ///     Represents an MSBuild list expression.
    /// </summary>
    public sealed class ExpressionList
        : ExpressionContainerNode, IPositionAware<ExpressionList>
    {
        /// <summary>
        ///     Create a new <see cref="ExpressionList"/>.
        /// </summary>
        public ExpressionList()
        {
        }

        /// <summary>
        ///     The node kind.
        /// </summary>
        public override ExpressionKind Kind => ExpressionKind.List;

        /// <summary>
        ///     The list's items.
        /// </summary>
        public IEnumerable<ExpressionNode> Items => Children;

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
        ExpressionList IPositionAware<ExpressionList>.SetPos(Position startPosition, int length)
        {
            SetPosition(startPosition, length);

            return this;
        }
    }
}
