using System;
using System.IO;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    /// <summary>
    ///     Helper functions for use in tests.
    /// </summary>
    static class TestHelper
    {
        /// <summary>
        ///     Attempt to find a file in the target directory or one of its ancestors.
        /// </summary>
        /// <param name="directory">
        ///     A <see cref="DirectoryInfo"/> representing the target directory.
        /// </param>
        /// <param name="fileName">
        ///     The name of the file to find.
        /// </param>
        /// <returns>
        ///     A <see cref="FileInfo"/> representing the file, if one was found; otherwise, <c>null</c>.
        /// </returns>
        public static FileInfo GetFileFromCurrentOrAncestor(this DirectoryInfo directory, string fileName)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(fileName)}.", nameof(fileName));

            FileInfo file = null;
            var targetDirectory = new DirectoryInfo(directory.FullName);
            while (file == null)
            {
                file = new FileInfo(
                    Path.Combine(targetDirectory.FullName, fileName)
                );
                if (file.Exists)
                    break;

                file = null;
                targetDirectory = targetDirectory.Parent;
                if (targetDirectory == null)
                    break;
            }

            return file;
        }

        /// <summary>
        ///     Attempt to find a file in the target directory or one of its ancestors.
        /// </summary>
        /// <param name="directory">
        ///     A <see cref="DirectoryInfo"/> representing the target directory.
        /// </param>
        /// <param name="relativePathName">
        ///     The relative path name of the file to find.
        /// </param>
        /// <returns>
        ///     A <see cref="FileInfo"/> representing the file, if one was found; otherwise, <c>null</c>.
        /// </returns>
        public static FileInfo GetFile(this DirectoryInfo directory, string relativePathName)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            if (string.IsNullOrWhiteSpace(relativePathName))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(relativePathName)}.", nameof(relativePathName));

            relativePathName = relativePathName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            string filePath = Path.Combine(directory.FullName, relativePathName);
            var file = new FileInfo(filePath);
            if (file.Exists)
                return file;

            return null;
        }
    }
}
