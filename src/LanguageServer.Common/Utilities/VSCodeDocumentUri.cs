using System;
using System.IO;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Helper methods for working with VSCode document <see cref="Uri"/>s.
    /// </summary>
    public static class VSCodeDocumentUri
    {

        /// <summary>
        ///     Convert a file-system path to a VSCode document URI.
        /// </summary>
        /// <param name="fileSystemPath">
        ///     The file-system path.
        /// </param>
        /// <returns>
        ///     The VSCode document URI.
        /// </returns>
        public static Uri FromFileSystemPath(string fileSystemPath)
        {
            if (string.IsNullOrWhiteSpace(fileSystemPath))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'fileSystemPath'.", nameof(fileSystemPath));

            if (!Path.IsPathRooted(fileSystemPath))
                throw new ArgumentException($"Path '{fileSystemPath}' is not an absolute path.", nameof(fileSystemPath));

            if (Path.DirectorySeparatorChar == '\\')
                return new Uri("file:///" + fileSystemPath.Replace('\\', '/'));

            return new Uri("file://" + fileSystemPath);
        }
    }
}
