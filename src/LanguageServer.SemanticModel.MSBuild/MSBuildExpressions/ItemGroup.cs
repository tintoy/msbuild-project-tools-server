using Sprache;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    /// <summary>
    ///     Represents an MSBuild item group expression.
    /// </summary>
    public class ItemGroup
        : ExpressionContainerNode, IPositionAware<ItemGroup>
    {
        /// <summary>
        ///     Does te item group expression have a name?
        /// </summary>
        public bool HasName => !string.IsNullOrWhiteSpace(Name);

        /// <summary>
        ///     The item group name.
        /// </summary>
        public string Name => Children.Count > 0 ? GetChild<Symbol>(0).Name : null;

        /// <summary>
        ///     Is the item group expression valid?
        /// </summary>
        public override bool IsValid => HasName && base.IsValid;

        /// <summary>
        ///     The node kind.
        /// </summary>
        public override ExpressionKind Kind => ExpressionKind.ItemGroup;

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
        ItemGroup IPositionAware<ItemGroup>.SetPos(Sprache.Position startPosition, int length)
        {
            SetPosition(startPosition, length);

            return this;
        }
    }
}
