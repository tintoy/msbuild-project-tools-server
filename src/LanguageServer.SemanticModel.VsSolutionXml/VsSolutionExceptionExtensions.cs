using Microsoft.VisualStudio.SolutionPersistence.Model;
using System;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Extension methods for MSBuild-related exceptions.
    /// </summary>
    public static class MSBuildExceptionExtensions
    {
        /// <summary>
        ///     Get the <see cref="Range"/> represented by the <see cref="SolutionException"/>.
        /// </summary>
        /// <param name="solutionException">
        ///     The <see cref="SolutionException"/>.
        /// </param>
        /// <param name="xmlLocator">
        ///     The XML locator API (if available).
        /// </param>
        /// <returns>
        ///     The <see cref="Range"/>.
        /// </returns>
        public static Range GetRange(this SolutionException solutionException, XmlLocator? xmlLocator)
        {
            if (solutionException == null)
                throw new ArgumentNullException(nameof(solutionException));

            var startPosition = new Position(
                solutionException.Line.GetValueOrDefault(),
                solutionException.Column.GetValueOrDefault()
            );            

            // Attempt to use the range of the actual XML that the exception refers to.
            XmlLocation? location = xmlLocator?.Inspect(startPosition);
            if (location != null)
                return location.Node.Range;

            // Otherwise, fall back to using the exception's declared start position...
            var endPosition = startPosition;

            // ...although it's sometimes less reliable.
            if (endPosition == Position.Zero)
                endPosition = startPosition;

            return new Range(startPosition, endPosition);
        }
    }
}
