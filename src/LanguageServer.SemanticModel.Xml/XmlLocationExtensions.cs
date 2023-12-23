using System;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Extension methods for <see cref="XmlLocation"/>.
    /// </summary>
    public static class XmlLocationExtensions
    {
        /// <summary>
        ///     Does the location represent a name?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element or attribute name; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsName(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.Flags.HasFlag(XmlLocationFlags.Name);
        }

        /// <summary>
        ///     Does the location represent a value?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents element content (text / whitespace) or an attribute value; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsValue(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.Flags.HasFlag(XmlLocationFlags.Value);
        }

        /// <summary>
        ///     Does the location represent text?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents text content within an element; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsText(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.Flags.HasFlag(XmlLocationFlags.Text);
        }

        /// <summary>
        ///     Does the location represent whitespace?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="whitespace">
        ///     Receives the <see cref="XSWhitespace"/> (if any) at the location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents whitespace within element content; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsWhitespace(this XmlLocation location, out XSWhitespace whitespace)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (location.IsWhitespace())
            {
                whitespace = (XSWhitespace)location.Node;

                return true;
            }

            whitespace = null;

            return false;
        }

        /// <summary>
        ///     Does the location represent whitespace?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents whitespace within element content; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsWhitespace(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.Flags.HasFlag(XmlLocationFlags.Whitespace);
        }

        /// <summary>
        ///     Does the location represent an attribute?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an attribute; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAttribute(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.Flags.HasFlag(XmlLocationFlags.Attribute);
        }

        /// <summary>
        ///     Does the location represent an attribute?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="attribute">
        ///     Receives the <see cref="XSAttribute"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an attribute; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAttribute(this XmlLocation location, out XSAttribute attribute)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (!location.IsAttribute())
            {
                attribute = null;

                return false;
            }

            attribute = (XSAttribute)location.Node;

            return true;
        }

        /// <summary>
        ///     Does the location represent an attribute's name?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an attribute's name; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAttributeName(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsAttribute() && location.IsName();
        }

        /// <summary>
        ///     Does the location represent an attribute's value?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an attribute's name; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAttributeValue(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsAttribute() && location.IsValue();
        }

        /// <summary>
        ///     Does the location represent an attribute's value?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="attribute">
        ///     Receives the attribute whose value is represented by the location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an attribute's name; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAttributeValue(this XmlLocation location, out XSAttribute attribute)
        {
            if (location.IsAttributeValue())
            {
                attribute = (XSAttribute)location.Node;

                return true;
            }

            attribute = null;

            return false;
        }

        /// <summary>
        ///     Does the location represent an element that has the specified attribute?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="attributeName">
        ///     The attribute name.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element with the specified attribute; otherwise, <c>false</c>.
        /// </returns>
        public static bool HasAttribute(this XmlLocation location, string attributeName)
        {
            return location.IsElement(out XSElement element) && element.HasAttribute(attributeName);
        }

        /// <summary>
        ///     Does the location represent an element that has the specified attribute?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="attributeName">
        ///     The attribute name.
        /// </param>
        /// <param name="attribute">
        ///     Receives the attribute.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element with the specified attribute; otherwise, <c>false</c>.
        /// </returns>
        public static bool HasAttribute(this XmlLocation location, string attributeName, out XSAttribute attribute)
        {
            if (location.IsElement(out XSElement element))
            {
                attribute = element[attributeName];

                return attribute != null;
            }

            attribute = null;

            return false;
        }

        /// <summary>
        ///     Does the location represent an element?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElement(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.Flags.HasFlag(XmlLocationFlags.Element);
        }

        /// <summary>
        ///     Does the location represent an element?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="element">
        ///     Receives the <see cref="XSElement"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElement(this XmlLocation location, out XSElement element)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (location.IsElement() && !location.IsElementContent())
            {
                element = (XSElement)location.Node;

                return true;
            }
            else
            {
                element = null;

                return false;
            }
        }

        /// <summary>
        ///     Does the location represent an empty element?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an empty element; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsEmptyElement(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsElement() && location.Flags.HasFlag(XmlLocationFlags.Empty);
        }

        /// <summary>
        ///     Does the location represent an empty element?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="emptyElement">
        ///     Receives the <see cref="XSEmptyElement"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an empty element; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsEmptyElement(this XmlLocation location, out XSEmptyElement emptyElement)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (!location.IsEmptyElement())
            {
                emptyElement = null;

                return false;
            }
            else
            {
                emptyElement = (XSEmptyElement)location.Node;

                return true;
            }
        }

        /// <summary>
        ///     Does the location represent an element content (i.e. text or whitespace)?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents element content; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementContent(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsElement() && location.IsValue();
        }

        /// <summary>
        ///     Does the location represent an element content (i.e. text or whitespace)?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="parentElement">
        ///     Receives the parent (i.e. containing) element.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents element content; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementContent(this XmlLocation location, out XSElement parentElement)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsElement(out parentElement) && location.IsValue();
        }

        /// <summary>
        ///     Does the location represent an element's textual content?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element's textual content; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementText(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsElement() && location.IsText();
        }

        /// <summary>
        ///     Does the location represent an element's textual content?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="text">
        ///     Receives the <see cref="XSElementText"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element's textual content; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementText(this XmlLocation location, out XSElementText text)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (location.IsElementText())
            {
                text = (XSElementText)location.Node;

                return true;
            }
            else
            {
                text = null;

                return false;
            }
        }

        /// <summary>
        ///     Does the location represent an element's opening tag?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element's opening tag; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementOpeningTag(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsElement() && location.Flags.HasFlag(XmlLocationFlags.OpeningTag);
        }

        /// <summary>
        ///     Does the location represent an element's closing tag?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element's closing tag; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementClosingTag(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsElement() && location.Flags.HasFlag(XmlLocationFlags.ClosingTag);
        }

        /// <summary>
        ///     Does the location represent an element's attributes range (but not a specific attribute)?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementBetweenAttributes(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsElement() && location.Flags.HasFlag(XmlLocationFlags.Attributes);
        }

        /// <summary>
        ///     Does the location represent an element's attributes range (but not a specific attribute)?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="element">
        ///     Receives the <see cref="XSElement"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementBetweenAttributes(this XmlLocation location, out XSElement element)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (location.IsElementBetweenAttributes())
            {
                element = (XSElement)location.Node;

                return true;
            }
            else
            {
                element = null;

                return false;
            }
        }

        /// <summary>
        ///     Does the location represent an element or an attribute?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element or attribute; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsElementOrAttribute(this XmlLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return location.IsElement() || location.IsAttribute();
        }

        /// <summary>
        ///     Does the location represent a place where an element can be created or replaced by a completion?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="replaceElement">
        ///     The element (if any) that will be replaced by the completion.
        /// </param>
        /// <param name="parentPath">
        ///     If specified, the location's node's parent must have the specified <see cref="XSPath"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element that can be replaced by completion; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        ///     We can replace "&lt;&gt;" and "&lt;&lt;Element /&gt;".
        /// </remarks>
        public static bool CanCompleteElement(this XmlLocation location, out XSElement replaceElement, XSPath parentPath = null)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            replaceElement = null;

            // Simplest case - we're on whitespace so we can simply insert an element without replacing anything.
            if (location.IsWhitespace(out XSWhitespace whitespace) && (parentPath == null || whitespace.HasParentPath(parentPath)))
                return true;

            if (!location.IsElement(out XSElement element))
                return false;

            if (location.IsElementBetweenAttributes())
                return false;

            // Check if we need to perform a substitution of the element to be replaced (the common case is simply replacing an existing element or partial element).
            if (element.IsValid)
            {
                // Do we have an invalid parent (e.g. "<<Foo />", which yields invalid element named "" with child element named "Foo")?
                bool isParentValid = element.ParentElement?.IsValid ?? true;
                if (!isParentValid)
                {
                    // We can't handle the case where the parent isn't on the same line as the child (since that's not the case outlined above).
                    if (element.ParentElement.Start.LineNumber != location.Node.Start.LineNumber)
                        return false;

                    // But we *can* handle this case by targeting the "parent" element (since that's the element we're actually after anyway).
                    if (location.Node.Start.ColumnNumber - element.ParentElement.Start.ColumnNumber == 1)
                        element = element.ParentElement;
                }
            }

            if (parentPath != null && !element.HasParentPath(parentPath))
                return false;

            replaceElement = element;

            return true;
        }

        /// <summary>
        ///     Does the location represent a place where an attribute can be created or replaced by a completion?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="element">
        ///     The element whose attribute will be completed.
        /// </param>
        /// <param name="replaceAttribute">
        ///     The attribute (if any) that will be replaced by the completion.
        /// </param>
        /// <param name="needsPadding">
        ///     An <see cref="PaddingType"/> value indicating what sort of padding (if any) is required before / after the attribute.
        /// </param>
        /// <param name="onElementWithPath">
        ///     If specified, the location's element must have the specified path.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an element that can be replaced by completion; otherwise, <c>false</c>.
        /// </returns>
        public static bool CanCompleteAttribute(this XmlLocation location, out XSElement element, out XSAttribute replaceAttribute, out PaddingType needsPadding, XSPath onElementWithPath = null)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            replaceAttribute = null;
            needsPadding = PaddingType.None;

            if (location.IsAttribute(out XSAttribute attribute) && !location.IsValue())
            {
                element = attribute.Element;
                if (location.Position == attribute.Start)
                {
                    // Since we're on an existing attribute, we'll add a new attribute after it.
                    attribute = null;
                    needsPadding = PaddingType.Trailing;
                }
            }
            else if (location.IsElementBetweenAttributes(out element))
            {
                if (element.Attributes.Count > 0)
                {
                    // Check if we're directly before an attribute.
                    foreach (XSAttribute currentAttribute in element.Attributes)
                    {
                        if (location.Position == currentAttribute.End)
                            needsPadding = PaddingType.Leading;
                        else
                            continue;

                        break;
                    }
                }
                else if (location.Position == element.NameRange.End) // We're directly after the name.
                    needsPadding = PaddingType.Leading;
            }
            else if (location.IsElement(out element))
            {
                // Check if we're directly after the name.
                if (location.Position != element.NameRange.End)
                    return false;

                needsPadding = PaddingType.Leading;
            }
            else
                return false;

            if (onElementWithPath != null && !element.Path.Matches(onElementWithPath))
                return false;

            replaceAttribute = attribute;

            return true;
        }

        /// <summary>
        ///     Does the location represent a place where an attribute value can be created or replaced by a completion?
        /// </summary>
        /// <param name="location">
        ///     The XML location.
        /// </param>
        /// <param name="targetAttribute">
        ///     The attribute (if any) whose value will be replaced by the completion.
        /// </param>
        /// <param name="onElementWithPath">
        ///     If specified, attribute's element must have the specified path.
        /// </param>
        /// <param name="forAttributeNamed">
        ///     If specified, the attribute must have one of the specified names.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the location represents an attribute whose value can be replaced by a completion; otherwise, <c>false</c>.
        /// </returns>
        public static bool CanCompleteAttributeValue(this XmlLocation location, out XSAttribute targetAttribute, XSPath onElementWithPath = null, params string[] forAttributeNamed)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            targetAttribute = null;

            if (!location.IsAttributeValue(out XSAttribute attribute))
                return false;

            if (onElementWithPath != null && !attribute.HasParentPath(onElementWithPath))
                return false;

            if (forAttributeNamed.Length > 0 && Array.IndexOf(forAttributeNamed, attribute.Name) == -1)
                return false;

            targetAttribute = attribute;

            return true;
        }
    }
}
