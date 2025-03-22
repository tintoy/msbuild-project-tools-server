using System;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     An object in a Visual Studio Solution.
    /// </summary>
    public abstract class VSSolutionObject
    {
        /// <summary>
        ///     Create a new <see cref="VSSolutionObject"/>.
        /// </summary>
        /// <param name="xml">
        ///     An <see cref="XSNode"/> representing the item's corresponding XML.
        /// </param>
        protected VSSolutionObject(XSNode xml)
        {
            if (xml == null)
                throw new ArgumentNullException(nameof(xml));

            Xml = xml;
        }

        /// <summary>
        ///     An <see cref="XSNode"/> representing the item's corresponding XML.
        /// </summary>
        public XSNode Xml { get; }

        /// <summary>
        ///     A <see cref="Range"/> representing the span of text covered by the item's XML.
        /// </summary>
        public Range XmlRange => Xml.Range;

        /// <summary>
        ///     The object's name.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        ///     The kind of Visual Studio Solution object represented by the <see cref="VSSolutionObject"/>.
        /// </summary>
        public abstract VSSolutionObjectKind Kind { get; }

        /// <summary>
        ///     The full path of the file where the object is declared.
        /// </summary>
        public abstract string SourceFile { get; }

        /// <summary>
        ///     Determine whether another <see cref="VSSolutionObject"/> represents the same underlying Visual Studio Solution object.
        /// </summary>
        /// <param name="other">
        ///     The <see cref="VSSolutionObject"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the 2 <see cref="VSSolutionObject"/>s represent the same underlying Visual Studio Solution object; otherwise, <c>false</c>.
        /// </returns>
        public abstract bool IsSameUnderlyingObject(VSSolutionObject other);

        /// <summary>
        ///     Determine whether the object's XML contains the specified position.
        /// </summary>
        /// <param name="position">
        ///     The target position.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the specified position lies within the object's XML span; otherwise, <c>false</c>.
        /// </returns>
        public bool XmlContains(Position position)
        {
            return XmlRange.Contains(position);
        }
    }

    /// <summary>
    ///     An object of a known type in a Visual Studio Solution.
    /// </summary>
    /// <typeparam name="TUnderlyingObject">
    ///     The type of underlying object represented by the <see cref="VSSolutionObject{TUnderlyingObject}"/>.
    /// </typeparam>
    public abstract class VSSolutionObject<TUnderlyingObject>
        : VSSolutionObject
    {
        /// <summary>
        ///     Create a new <see cref="VSSolutionObject{TUnderlyingObject}"/>.
        /// </summary>
        /// <param name="underlyingObject">
        ///     The underlying Visual Studio Solution object.
        /// </param>
        /// <param name="declaringXml">
        ///     An <see cref="XSNode"/> representing the object's declaring XML.
        /// </param>
        protected VSSolutionObject(TUnderlyingObject underlyingObject, XSNode declaringXml)
            : base(declaringXml)
        {
            if (underlyingObject == null)
                throw new ArgumentNullException(nameof(underlyingObject));

            UnderlyingObject = underlyingObject;
        }

        /// <summary>
        ///     The underlying Visual Studio Solution object.
        /// </summary>
        protected TUnderlyingObject UnderlyingObject { get; }

        /// <summary>
        ///     Determine whether another <see cref="VSSolutionObject"/> represents the same underlying Visual Studio Solution object.
        /// </summary>
        /// <param name="other">
        ///     The <see cref="VSSolutionObject"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the 2 <see cref="VSSolutionObject"/>s represent the same underlying Visual Studio Solution object; otherwise, <c>false</c>.
        /// </returns>
        public sealed override bool IsSameUnderlyingObject(VSSolutionObject other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (other is VSSolutionObject<TUnderlyingObject> otherWithUnderlying)
                return ReferenceEquals(UnderlyingObject, otherWithUnderlying.UnderlyingObject);

            return false;
        }
    }
}
