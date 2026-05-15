using System;
using System.IO;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Helper methods for working with files and streams.
    /// </summary>
    public static class IOHelper
    {
        /// <summary>
        ///     The default buffer size for <see cref="FileStream"/>s.
        /// </summary>
        public const int DefaultFileStreamBufferSize = 2048;

        /// <summary>
        ///     Open a file for reading asynchronously (<see cref="FileOptions.Asynchronous"/>).
        /// </summary>
        /// <param name="file">
        ///     A <see cref="FileInfo"/> representing the file to open.
        /// </param>
        /// <param name="sharing">
        ///     A <see cref="FileShare"/> value that indicates the level of access other processes should have to the target file.
        /// </param>
        /// <param name="bufferSize">
        ///     The buffer size to be used by the <see cref="FileStream"/>.
        /// </param>
        /// <param name="additionalOptions">
        ///     Additional <see cref="FileOptions"/> flags (if any) that can be used to customise the files
        /// </param>
        /// <returns>
        ///     A new <see cref="FileStream"/> that can be used to read from the file.
        /// </returns>
        public static FileStream OpenAsyncRead(this FileInfo file, FileShare sharing = FileShare.Read, int bufferSize = DefaultFileStreamBufferSize, FileOptions additionalOptions = FileOptions.None)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            FileOptions options = FileOptions.Asynchronous | additionalOptions;

            return new FileStream(file.FullName, FileMode.Open, FileAccess.Read, sharing, bufferSize, options);
        }
    }
}
