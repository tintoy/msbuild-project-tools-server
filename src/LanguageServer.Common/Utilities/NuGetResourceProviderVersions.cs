using System;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Well-known versions of the NuGet resource providers.
    /// </summary>
    [Flags]
    public enum NuGetResourceProviderVersions
    {
        /// <summary>
        ///     No resource providers.
        /// </summary>
        None    = 0,

        /// <summary>
        ///     Version 2 of the NuGet providers.
        /// </summary>
        /// <remarks>
        ///     No longer supported by the version of the NuGet client libraries in use by MSBuild Project Tools.
        /// </remarks>
        V2      = 1,

        /// <summary>
        ///     Version 3 of the NuGet providers.
        /// </summary>
        V3      = 2,

        /// <summary>
        /// The currently supported version(s) of the NuGet providers.
        /// </summary>
        Current = V3
    }
}
