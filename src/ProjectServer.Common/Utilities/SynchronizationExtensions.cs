using System;

namespace MSBuildProjectTools.ProjectServer.Utilities
{
    /// <summary>
    ///     Extension methods for synchronisation (i.e. concurrency) scenarios.
    /// </summary>
    public static partial class SynchronizationExtensions
    {
        /// <summary>
        ///     The default span of time to wait for a lock before timing out.
        /// </summary>
        public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(30);
    }
}
