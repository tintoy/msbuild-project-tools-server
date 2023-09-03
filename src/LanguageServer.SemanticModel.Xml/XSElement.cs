using Microsoft.Language.Xml;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents an XML element.
    /// </summary>
    public abstract class XSElement
        : XSNode<XmlElementSyntaxBase>
    {
        /// <summary>
        ///     The element's path within the XML.
        /// </summary>
        private readonly XSPath _path;

        /// <summary>
        ///     Create a new <see cref="XSElement"/>.
        /// </summary>
        /// <param name="element">
        ///     The <see cref="XmlElementSyntaxBase"/> represented by the <see cref="XSElement"/>.
        /// </param>
        /// <param name="range">
        ///     The range, within the source text, spanned by the element.
        /// </param>
        /// <param name="nameRange">
        ///     The range, within the source text, spanned by the element's name.
        /// </param>
        /// <param name="attributesRange">
        ///     The range, within the source text, spanned by the element's attributes.
        /// </param>
        /// <param name="parent">
        ///     The <see cref="XSElement"/>'s parent element (if any).
        /// </param>
        protected XSElement(XmlElementSyntaxBase element, Range range, Range nameRange, Range attributesRange, XSElement parent)
            : base(element, range)
        {
            NameRange = nameRange;
            AttributesRange = attributesRange;
            ParentElement = parent;

            XSPath parentPath = parent?.Path ?? XSPath.Root;
            _path = parentPath + Name;
        }

        /// <summary>
        ///     The element name.
        /// </summary>
        public override string Name => SyntaxNode.Name;

        /// <summary>
        ///     The element name prefix (if any).
        /// </summary>
        public string Prefix => SyntaxNode.NameNode?.Prefix?.Name?.Text;

        /// <summary>
        ///     The <see cref="XmlElementSyntaxBase"/> represented by the <see cref="XSElement"/>.
        /// </summary>
        public XmlElementSyntaxBase ElementNode => SyntaxNode;

        /// <summary>
        ///     The range, within the source text, spanned by the element's name.
        /// </summary>
        public Range NameRange { get; }

        /// <summary>
        ///     The range, within the source text, spanned by the element's attributes.
        /// </summary>
        public Range AttributesRange { get; }

        /// <summary>
        ///     The element's path within the XML.
        /// </summary>
        public override XSPath Path => _path;

        /// <summary>
        ///     The <see cref="XSElement"/>'s parent element (if any).
        /// </summary>
        public XSElement ParentElement { get; internal set; }

        /// <summary>
        ///     The element's attributes (if any).
        /// </summary>
        public ImmutableList<XSAttribute> Attributes { get; internal set; } = ImmutableList<XSAttribute>.Empty;

        /// <summary>
        ///     The names of the element's attributes.
        /// </summary>
        public IEnumerable<string> AttributeNames => Attributes.Select(attribute => attribute.Name);

        /// <summary>
        ///     The element's content (if any).
        /// </summary>
        public ImmutableList<XSNode> Content { get; internal set; } = ImmutableList<XSNode>.Empty;

        /// <summary>
        ///     The element's child elements (if any).
        /// </summary>
        public IEnumerable<XSElement> ChildElements => Content.OfType<XSElement>();

        /// <summary>
        ///     The kind of XML node represented by the <see cref="XSNode"/>.
        /// </summary>
        public override XSNodeKind Kind => XSNodeKind.Element;

        /// <summary>
        ///     Does the <see cref="XSNode"/> represent valid XML?
        /// </summary>
        public override bool IsValid => true;

        /// <summary>
        ///     Does the <see cref="XSElement"/> have any content (besides attributes).
        /// </summary>
        public abstract bool HasContent { get; }

        /// <summary>
        ///     Does the element have any attributes?
        /// </summary>
        public bool HasAttributes => SyntaxNode.Attributes.Any();

        /// <summary>
        ///     Get the first attribute (if any) with the specified name.
        /// </summary>
        /// <param name="attributeName">
        ///     The attribute name.
        /// </param>
        /// <returns>
        ///     The <see cref="XSAttribute"/>, or <c>null</c> if no attribute was found with the specified name.
        /// </returns>
        public XSAttribute this[string attributeName]
        {
            get => Attributes.FirstOrDefault(attribute => attribute.Name == attributeName);
        }

        /// <summary>
        ///     Does the element have an attribute with the specified name?
        /// </summary>
        /// <param name="attributeName">
        ///     The attribute name.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the element has the specified attribute; otherwise, <c>false</c>.
        /// </returns>
        public bool HasAttribute(string attributeName) => Attributes.Any(attribute => attribute.Name == attributeName);
    }
}
