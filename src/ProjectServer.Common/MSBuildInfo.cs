using Microsoft.Build.Locator;
using MSBuildProjectTools.ProjectServer.Utilities;
using NuGet.Versioning;
using System;
using System.Diagnostics;
using System.IO;

#pragma warning disable IDE0007 // Use implicit type

namespace MSBuildProjectTools.ProjectServer
{
    /// <summary>
    ///     Information about an instance of the MSBuild engine.
    /// </summary>
    /// <param name="BaseDirectory">
    ///     The base directory where the MSBuild instance is located.
    /// </param>
    /// <param name="Version">
    ///     The version of the MSBuild engine represented by the instance.
    /// </param>
    /// <param name="Sdk">
    ///     Information about the .NET SDK (if any) that provides the MSBuild engine.
    /// </param>
    public record class MSBuildInfo(string BaseDirectory, SemanticVersion Version, DotnetSdkInfo Sdk)
    {
        /// <summary>
        ///     Is the MSBuild instance provided by a .NET SDK?
        /// </summary>
        public bool IsFromSdk => HasSdk;
        
        /// <summary>
        ///     Does the <see cref="MSBuildInfo"/> have a valid <see cref="BaseDirectory"/>?
        /// </summary>
        public bool HasBaseDirectory => !String.IsNullOrWhiteSpace(BaseDirectory);

        /// <summary>
        ///     Does the <see cref="MSBuildInfo"/> have a valid <see cref="Version"/>?
        /// </summary>
        public bool HasVersion => Version != EmptyVersion;

        /// <summary>
        ///     Does the <see cref="MSBuildInfo"/> have a valid <see cref="Sdk"/>?
        /// </summary>
        public bool HasSdk => Sdk != null;

        /// <summary>
        ///     Does the <see cref="MSBuildInfo"/> represent an unknown or missing SDK?
        /// </summary>
        public bool IsEmpty => !HasBaseDirectory && !HasVersion && !HasSdk;

        /// <summary>
        ///     Get the runtime version (if any) as a string.
        /// </summary>
        /// <returns>
        ///     The runtime <see cref="Version"/> as a string, or <c>null</c> if the runtime <see cref="Version"/> is empty.
        /// </returns>
        public string? GetVersionString() => (Version != EmptyVersion) ? Version.ToString() : null;

        /// <summary>
        ///     The <see cref="NuGet.Versioning.SemanticVersion"/> representing an unknown MSBuild version.
        /// </summary>
        static readonly SemanticVersion EmptyVersion = new SemanticVersion(0, 0, 0);

        /// <summary>
        ///     <see cref="DotnetRuntimeInfo"/> representing an unknown or missing MSBuild instance.
        /// </summary>
        public static readonly MSBuildInfo Empty = new MSBuildInfo(BaseDirectory: String.Empty, Version: EmptyVersion, DotnetSdkInfo.Empty);

        /// <summary>
        ///     Create <see cref="MSBuildInfo"/> to from an MSBuildLocator <see cref="VisualStudioInstance"/>.
        /// </summary>
        /// <param name="discoveredMSBuild">
        ///     A <see cref="VisualStudioInstance"/> representing a discovered MSBuild instance.
        /// </param>
        /// <returns>
        ///     The new <see cref="MSBuildInfo"/>.
        /// </returns>
        public static MSBuildInfo From(VisualStudioInstance discoveredMSBuild)
        {
            if (discoveredMSBuild == null)
                throw new ArgumentNullException(nameof(discoveredMSBuild));

            switch (discoveredMSBuild.DiscoveryType)
            {
                case DiscoveryType.DotNetSdk:
                {
                    SemanticVersion msbuildVersion = new SemanticVersion(0, 0, 0);
                    DotnetSdkInfo discoveredSdk = DotnetSdkInfo.Empty;

                    string msbuildAssemblyFile = Path.Combine(discoveredMSBuild.VisualStudioRootPath, "Microsoft.Build.dll");
                    if (File.Exists(msbuildAssemblyFile))
                    {
                        FileVersionInfo msbuildVersionInfo = FileVersionInfo.GetVersionInfo(msbuildAssemblyFile);
                        if (!String.IsNullOrWhiteSpace(msbuildVersionInfo.ProductVersion))
                        {
                            msbuildVersion = new SemanticVersion(discoveredMSBuild.Version.Major, discoveredMSBuild.Version.Minor, discoveredMSBuild.Version.Revision);

                            discoveredSdk = new DotnetSdkInfo(
                                Version: SemanticVersion.Parse(msbuildVersionInfo.ProductVersion),
                                BaseDirectory: discoveredMSBuild.VisualStudioRootPath
                            );
                        }
                    }

                    return new MSBuildInfo(
                        BaseDirectory: discoveredMSBuild.MSBuildPath,
                        Version: msbuildVersion,
                        Sdk: discoveredSdk
                    );
                }
                default:
                {
                    return new MSBuildInfo(
                        BaseDirectory: discoveredMSBuild.MSBuildPath,
                        Version: new SemanticVersion(discoveredMSBuild.Version.Major, discoveredMSBuild.Version.Minor, discoveredMSBuild.Version.Revision),
                        Sdk: DotnetSdkInfo.Empty
                    );
                }
            }
        }
    }
}
