using MSBuildProjectTools.LanguageServer.Utilities;
using System;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     An object in a solution.
    /// </summary>
    public abstract class VsSolutionObject
    {
        /// <summary>
        ///     Create a new <see cref="VsSolutionObject"/>.
        /// </summary>
        /// <param name="solution">
        ///     The <see cref="VsSolution"/> that contains the underlying object.
        /// </param>
        /// <param name="xml">
        ///     An <see cref="XSNode"/> representing the item's corresponding XML.
        /// </param>
        protected VsSolutionObject(VsSolution solution, XSNode xml)
        {
            if (solution == null)
                throw new ArgumentNullException(nameof(solution));

            if (xml == null)
                throw new ArgumentNullException(nameof(xml));

            Solution = solution;
            Xml = xml;
        }

        /// <summary>
        ///     The <see cref="VsSolution"/> that contains the underlying object.
        /// </summary>
        public VsSolution Solution { get; }

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
        ///     The kind of solution object represented by the <see cref="VsSolutionObject"/>.
        /// </summary>
        public abstract VsSolutionObjectKind Kind { get; }

        /// <summary>
        ///     The full path of the file where the object is declared.
        /// </summary>
        public abstract string SourceFile { get; }

        /// <summary>
        ///     Determine whether another <see cref="VsSolutionObject"/> represents the same underlying VsSolution object.
        /// </summary>
        /// <param name="other">
        ///     The <see cref="VsSolutionObject"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the 2 <see cref="VsSolutionObject"/>s represent the same underlying VsSolution object; otherwise, <c>false</c>.
        /// </returns>
        public abstract bool IsSameUnderlyingObject(VsSolutionObject other);

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
    ///     An object of a known type in a solution.
    /// </summary>
    /// <typeparam name="TUnderlyingObject">
    ///     The type of underlying object represented by the <see cref="VsSolutionObject{TUnderlyingObject}"/>.
    /// </typeparam>
    public abstract class VsSolutionObject<TUnderlyingObject>
        : VsSolutionObject
    {
        /// <summary>
        ///     Create a new <see cref="VsSolutionObject{TUnderlyingObject}"/>.
        /// </summary>
        /// <param name="solution">
        ///     The <see cref="VsSolution"/> that contains the underlying object.
        /// </param>
        /// <param name="underlyingObject">
        ///     The underlying VsSolution object.
        /// </param>
        /// <param name="declaringXml">
        ///     An <see cref="XSNode"/> representing the object's declaring XML.
        /// </param>
        protected VsSolutionObject(VsSolution solution, TUnderlyingObject underlyingObject, XSNode declaringXml)
            : base(solution, declaringXml)
        {
            if (underlyingObject == null)
                throw new ArgumentNullException(nameof(underlyingObject));

            UnderlyingObject = underlyingObject;
        }

        /// <summary>
        ///     The underlying VsSolution object.
        /// </summary>
        protected TUnderlyingObject UnderlyingObject { get; }

        /// <summary>
        ///     Determine whether another <see cref="VsSolutionObject"/> represents the same underlying VsSolution object.
        /// </summary>
        /// <param name="other">
        ///     The <see cref="VsSolutionObject"/>.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the 2 <see cref="VsSolutionObject"/>s represent the same underlying VsSolution object; otherwise, <c>false</c>.
        /// </returns>
        public sealed override bool IsSameUnderlyingObject(VsSolutionObject other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (other is VsSolutionObject<TUnderlyingObject> otherWithUnderlying)
                return ReferenceEquals(UnderlyingObject, otherWithUnderlying.UnderlyingObject);

            return false;
        }
    }
}
