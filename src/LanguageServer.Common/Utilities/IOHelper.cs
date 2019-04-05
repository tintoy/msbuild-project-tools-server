using System;
using System.IO;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Helper methods for working with files, directories, etc.
    /// </summary>
    public static class IOHelper
    {
        /// <summary>
        ///     Watch for changes to the file.
        /// </summary>
        /// <param name="file">
        ///     A <see cref="FileInfo"/> representing the file to watch.
        /// </param>
        /// <param name="notifyFilter">
        ///     Optional <see cref="NotifyFilters"/> flags indicating the kinds of changes to watch for (default is <see cref="NotifyFilters.CreationTime"/> | <see cref="NotifyFilters.LastWrite"/>).
        /// </param>
        /// <returns>
        ///     A configured <see cref="FileSystemWatcher"/> that will raise events when the file is changed.
        /// </returns>
        public static FileSystemWatcher Watch(this FileInfo file, NotifyFilters notifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            
            var watcher = new FileSystemWatcher(file.FullName);
            try
            {
                watcher.NotifyFilter = notifyFilter;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception)
            {
                using (watcher)
                    throw;
            }

            return watcher;
        }
    }
}
