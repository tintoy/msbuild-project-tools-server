using System;
using System.IO;

namespace MSBuildProjectTools.LanguageServer.Documents
{
    /// <summary>
    ///     Helper methods for working with <see cref="Document"/>s.
    /// </summary>
    public static class DocumentHelper
    {
        /// <summary>
        ///     Determine the kind of document represented by the specified file path.
        /// </summary>
        /// <param name="documentPath">
        ///     The document file path.
        /// </param>
        /// <returns>
        ///     A <see cref="Documents.DocumentKind"/> value indicating the document kind.
        /// </returns>
        /// <remarks>
        ///     <b>TODO:</b>
        ///     <list type="bullet">
        ///         <item>Reimplement this logic to use both document path and language identifier (<seealso cref="LanguageIdentifiers"/>) so we can match against known <see cref="DocumentSelectors"/> in order to determine the document kind.</item>
        ///         <item>We can also use Microsoft.Extensions.FileSystemGlobbing to handle matching of the file path.</item>
        ///     </list>
        /// </remarks>
        public static DocumentKind GetDocumentKind(string documentPath)
        {
            if (string.IsNullOrWhiteSpace(documentPath))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(documentPath)}.", nameof(documentPath));

            string fileExension = Path.GetExtension(documentPath)?.ToLowerInvariant();
            if (String.IsNullOrWhiteSpace(fileExension))
                return DocumentKind.Unknown;

            if (fileExension == ".slnx")
                return DocumentKind.Solution;

            if (fileExension.EndsWith("proj"))
                return DocumentKind.Project;

            if (fileExension == ".props" || fileExension == ".targets")
                return DocumentKind.Project;

            return DocumentKind.Unknown;
        }
    }
}
