using System;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents non-significant whitespace (the syntax model refers to this as whitespace trivia).
    /// </summary>
    public class XSWhitespace
        : XSNode
    {
        /// <summary>
        ///     The whitespace's path within the XML.
        /// </summary>
        private readonly XSPath _path;

        /// <summary>
        ///     Create new <see cref="XSWhitespace"/>.
        /// </summary>
        /// <param name="range">
        ///     The <see cref="Range"/>, within the source text, spanned by the whitespace.
        /// </param>
        /// <param name="parent">
        ///     The <see cref="XSElement"/> that contains the whitespace.
        /// </param>
        public XSWhitespace(Range range, XSElement parent)
            : base(range)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            ParentElement = parent;

            XSPath parentPath = parent?.Path ?? XSPath.Root;
            _path = parentPath + Name;
        }

        /// <summary>
        ///     The kind of <see cref="XSNode"/>.
        /// </summary>
        public override XSNodeKind Kind => XSNodeKind.Whitespace;

        /// <summary>
        ///     The node name.
        /// </summary>
        public override string Name => "#whitespace";

        /// <summary>
        ///     The whitespace's path within the XML.
        /// </summary>
        public override XSPath Path => _path;

        /// <summary>
        ///     The <see cref="XSElement"/> that contains the whitespace.
        /// </summary>
        public XSElement ParentElement { get; }

        /// <summary>
        ///     Does the <see cref="XSNode"/> represent valid XML?
        /// </summary>
        public override bool IsValid => true;
    }
}
