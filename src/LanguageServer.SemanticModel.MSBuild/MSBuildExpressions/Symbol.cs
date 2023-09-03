using Sprache;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    /// <summary>
    ///     Represents an MSBuild comparison expression.
    /// </summary>
    public class Symbol
        : ExpressionNode, IPositionAware<Symbol>
    {
        /// <summary>
        ///     The node kind.
        /// </summary>
        public override ExpressionKind Kind => ExpressionKind.Symbol;

        /// <summary>
        ///     The symbol's name.
        /// </summary>
        public string Name { get; internal set; } = string.Empty;

        /// <summary>
        ///     The symbol's namespace.
        /// </summary>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        ///     The symbol's fully-qualified name.
        /// </summary>
        public string FullName => IsQualified ? string.Format("{0}.{1}", Namespace, Name) : Name;

        /// <summary>
        ///     Is the symbol qualified (i.e. does it have a namespace)?
        /// </summary>
        public bool IsQualified => !string.IsNullOrWhiteSpace(Namespace);

        /// <summary>
        ///     Is the symbol valid?
        /// </summary>
        public override bool IsValid => !string.IsNullOrWhiteSpace(Name) && base.IsValid;

        /// <summary>
        ///     Get a string representation of the expression node.
        /// </summary>
        /// <returns>
        ///     The string representation.
        /// </returns>
        public override string ToString() => $"MSBuild Symbol ({FullName}) @ {Range}";

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
        Symbol IPositionAware<Symbol>.SetPos(Sprache.Position startPosition, int length)
        {
            SetPosition(startPosition, length);

            return this;
        }
    }
}
