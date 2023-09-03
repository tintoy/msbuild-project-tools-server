using Microsoft.Language.Xml;
using System;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents an XML attribute.
    /// </summary>
    public class XSAttribute
        : XSNode<XmlAttributeSyntax>
    {
        /// <summary>
        ///     The attribute's path within the XML.
        /// </summary>
        private readonly XSPath _path;

        /// <summary>
        ///     Create a new <see cref="XSAttribute"/>.
        /// </summary>
        /// <param name="attribute">
        ///     The <see cref="XmlAttributeSyntax"/> represented by the <see cref="XSAttribute"/>.
        /// </param>
        /// <param name="element">
        ///     The element that contains the attribute.
        /// </param>
        /// <param name="attributeRange">
        ///     The <see cref="Range"/>, within the source text, spanned by the attribute.
        /// </param>
        /// <param name="nameRange">
        ///     The <see cref="Range"/>, within the source text, spanned by the attribute's name.
        /// </param>
        /// <param name="valueRange">
        ///     The <see cref="Range"/>, within the source text, spanned by the attribute's value.
        /// </param>
        public XSAttribute(XmlAttributeSyntax attribute, XSElement element, Range attributeRange, Range nameRange, Range valueRange)
            : base(attribute, attributeRange)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            NameRange = nameRange;
            ValueRange = valueRange;
            Element = element;
            _path = Element.Path + Name;
        }

        /// <summary>
        ///     The attribute name.
        /// </summary>
        public override string Name => AttributeNode.Name;

        /// <summary>
        ///     The attribute name prefix (if any).
        /// </summary>
        public string Prefix => SyntaxNode.NameNode?.Prefix?.Name?.Text;

        /// <summary>
        ///     The attribute value.
        /// </summary>
        public string Value => AttributeNode.Value;

        /// <summary>
        ///     Does the attribute represent the default XML namespace, or an XML namespace prefix?
        /// </summary>
        public bool IsNamespace => IsDefaultNamespace || IsNamespacePrefix;

        /// <summary>
        ///     Does the attribute represent the default XML namespace?
        /// </summary>
        public bool IsDefaultNamespace => Name == "xmlns";

        /// <summary>
        ///     Does the attribute represent an XML namespace prefix?
        /// </summary>
        public bool IsNamespacePrefix => Prefix == "xmlns";

        /// <summary>
        ///     The <see cref="XmlAttributeSyntax"/> represented by the <see cref="XSAttribute"/>.
        /// </summary>
        public XmlAttributeSyntax AttributeNode => SyntaxNode;

        /// <summary>
        ///     The element that contains the attribute.
        /// </summary>
        public XSElement Element { get; }

        /// <summary>
        ///     The <see cref="Range"/>, within the source text, spanned by the attribute's name.
        /// </summary>
        public Range NameRange { get; }

        /// <summary>
        ///     The <see cref="Range"/>, within the source text, spanned by the attribute's value.
        /// </summary>
        public Range ValueRange { get; }

        /// <summary>
        ///     The attribute's path within the XML.
        /// </summary>
        public override XSPath Path => _path;

        /// <summary>
        ///     The kind of XML node represented by the <see cref="XSNode"/>.
        /// </summary>
        public override XSNodeKind Kind => XSNodeKind.Attribute;

        /// <summary>
        ///     Does the <see cref="XSNode"/> represent valid XML?
        /// </summary>
        public override bool IsValid => true;
    }
}
