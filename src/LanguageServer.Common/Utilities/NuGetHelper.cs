using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Helper methods for interacting with the NuGet API.
    /// </summary>
    public static class NuGetHelper
    {
        /// <summary>
        ///     Get all package sources configured for the specified workspace.
        /// </summary>
        /// <param name="workspaceRootDirectory">
        ///     The workspace's root directory.
        /// </param>
        /// <returns>
        ///     A list of configured package sources.
        /// </returns>
        public static List<PackageSource> GetWorkspacePackageSources(string workspaceRootDirectory)
        {
            if (String.IsNullOrWhiteSpace(workspaceRootDirectory))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'workspaceRootDirectory'.", nameof(workspaceRootDirectory));

            return new List<PackageSource>(
                new PackageSourceProvider(
                    Settings.LoadDefaultSettings(workspaceRootDirectory)
                )
                .LoadPackageSources()
            );
        }

        /// <summary>
        /// Create NuGet resource providers.
        /// </summary>
        /// <param name="providerVersions">The provider version(s) to create.</param>
        /// <returns>A list of resource providers.</returns>
        public static List<Lazy<INuGetResourceProvider>> CreateResourceProviders(NuGetResourceProviderVersions providerVersions = NuGetResourceProviderVersions.Current)
        {
            var providers = new List<Lazy<INuGetResourceProvider>>();

            if ((providerVersions & NuGetResourceProviderVersions.V3) != 0)
            {
                // v3 API support
                providers.AddRange(Repository.Provider.GetCoreV3());
            }

            return providers;
        }

        /// <summary>
        /// Create resource repositories for the specified package source.
        /// </summary>
        /// <param name="packageSources">The <see cref="PackageSource"/>s.</param>
        /// <param name="providerVersions">The NuGet provider version(s) to use.</param>
        /// <returns>A list of configured <see cref="SourceRepository"/> instances (one for each <see cref="PackageSource"/>).</returns>
        public static List<SourceRepository> CreateResourceRepositories(this IEnumerable<PackageSource> packageSources, NuGetResourceProviderVersions providerVersions = NuGetResourceProviderVersions.Current)
        {
            if (packageSources == null)
                throw new ArgumentNullException(nameof(packageSources));

            List<SourceRepository> sourceRepositories = new List<SourceRepository>();

            List<Lazy<INuGetResourceProvider>> providers = CreateResourceProviders(providerVersions);

            foreach (PackageSource packageSource in packageSources)
            {
                SourceRepository sourceRepository = new SourceRepository(packageSource, providers);

                sourceRepositories.Add(sourceRepository);
            }

            return sourceRepositories;
        }

        /// <summary>
        /// Create v3 resource repositories for the specified package source.
        /// </summary>
        /// <param name="packageSource">The <see cref="PackageSource"/>.</param>
        /// <param name="providerVersions">A <see cref="NuGetResourceProviderVersions"/> value indicating which versions of the NuGet resource providers to use.</param>
        /// <returns>The configured <see cref="SourceRepository"/> instance.</returns>
        public static SourceRepository CreateResourceRepository(this PackageSource packageSource, NuGetResourceProviderVersions providerVersions = NuGetResourceProviderVersions.Current)
        {
            if (packageSource == null)
                throw new ArgumentNullException(nameof(packageSource));

            return packageSource.CreateResourceRepository(
                providers: CreateResourceProviders(providerVersions)
            );
        }

        /// <summary>
        /// Create resource repositories for the specified package source.
        /// </summary>
        /// <param name="packageSource">The <see cref="PackageSource"/>.</param>
        /// <param name="providers">The NuGet resource providers to be used by the repository.</param>
        /// <returns>The configured <see cref="SourceRepository"/> instance.</returns>
        public static SourceRepository CreateResourceRepository(this PackageSource packageSource, List<Lazy<INuGetResourceProvider>> providers)
        {
            if (packageSource == null)
                throw new ArgumentNullException(nameof(packageSource));

            if (providers == null)
                throw new ArgumentNullException(nameof(providers));

            SourceRepository sourceRepository = new SourceRepository(packageSource, providers);

            return sourceRepository;
        }

        /// <summary>
        ///     Get NuGet AutoComplete APIs for the specified package source URLs.
        /// </summary>
        /// <param name="packageSourceUrls">
        ///     The package source URLs.
        /// </param>
        /// <returns>
        ///     A task that resolves to a list of <see cref="AutoCompleteResource"/>s.
        /// </returns>
        public static Task<List<AutoCompleteResource>> GetAutoCompleteResources(params string[] packageSourceUrls)
        {
            return GetAutoCompleteResources(
                packageSourceUrls.Select(packageSourceUrl => new PackageSource(packageSourceUrl))
            );
        }

        /// <summary>
        ///     Get NuGet AutoComplete APIs for the specified package sources.
        /// </summary>
        /// <param name="packageSources">
        ///     The package sources.
        /// </param>
        /// <returns>
        ///     A task that resolves to a list of <see cref="AutoCompleteResource"/>s.
        /// </returns>
        public static Task<List<AutoCompleteResource>> GetAutoCompleteResources(params PackageSource[] packageSources)
        {
            return GetAutoCompleteResources(
                (IEnumerable<PackageSource>)packageSources
            );
        }

        /// <summary>
        ///     Get NuGet AutoComplete APIs for the specified package sources.
        /// </summary>
        /// <param name="packageSources">
        ///     The package sources.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task that resolves to a list of <see cref="AutoCompleteResource"/>s.
        /// </returns>
        public static async Task<List<AutoCompleteResource>> GetAutoCompleteResources(IEnumerable<PackageSource> packageSources, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (packageSources == null)
                throw new ArgumentNullException(nameof(packageSources));

            List<AutoCompleteResource> autoCompleteResources = new List<AutoCompleteResource>();

            List<SourceRepository> sourceRepositories = packageSources.CreateResourceRepositories();
            foreach (SourceRepository sourceRepository in sourceRepositories)
            {
                AutoCompleteResource autoCompleteResource = await sourceRepository.GetResourceAsync<AutoCompleteResource>(cancellationToken);
                if (autoCompleteResource != null)
                    autoCompleteResources.Add(autoCompleteResource);
            }

            return autoCompleteResources;
        }

        /// <summary>
        ///     Suggest package Ids based on a prefix.
        /// </summary>
        /// <param name="autoCompleteResources">
        ///     The <see cref="AutoCompleteResource"/>s used to retrieve suggestions.
        /// </param>
        /// <param name="packageIdPrefix">
        ///     The package Id prefix to match.
        /// </param>
        /// <param name="includePrerelease">
        ///     Include packages with only pre-release versions available?
        /// </param>
        /// <param name="logger">
        ///     An optional NuGet logger to be used for reporting errors / progress (etc).
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A sorted set of suggested package Ids.
        /// </returns>
        public static async Task<SortedSet<string>> SuggestPackageIds(this IEnumerable<AutoCompleteResource> autoCompleteResources, string packageIdPrefix, bool includePrerelease = false, ILogger logger = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (autoCompleteResources == null)
                throw new ArgumentNullException(nameof(autoCompleteResources));

            if (packageIdPrefix == null)
                throw new ArgumentNullException(nameof(packageIdPrefix));

            IEnumerable<string>[] results = await Task.WhenAll(
                autoCompleteResources.Select(
                    autoCompleteResource => autoCompleteResource.IdStartsWith(packageIdPrefix, includePrerelease, logger ?? NullLogger.Instance, cancellationToken)
                )
            );

            return new SortedSet<string>(
                results.Flatten()
            );
        }

        /// <summary>
        ///     Suggest versions for the specified package.
        /// </summary>
        /// <param name="autoCompleteResources">
        ///     The <see cref="AutoCompleteResource"/>s used to retrieve suggestions.
        /// </param>
        /// <param name="versionPrefix">
        ///     An optional version prefix to match.
        /// </param>
        /// <param name="packageId">
        ///     The package Id to match.
        /// </param>
        /// <param name="includePrerelease">
        ///     Include pre-release versions?
        /// </param>
        /// <param name="logger">
        ///     An optional NuGet logger to be used for reporting progress (etc).
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A sorted set of suggested package versions.
        /// </returns>
        public static async Task<SortedSet<NuGetVersion>> SuggestPackageVersions(this IEnumerable<AutoCompleteResource> autoCompleteResources, string packageId, bool includePrerelease = false, string versionPrefix = "", ILogger logger = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (autoCompleteResources == null)
                throw new ArgumentNullException(nameof(autoCompleteResources));

            if (packageId == null)
                throw new ArgumentNullException(nameof(packageId));

            IEnumerable<NuGetVersion>[] results = await Task.WhenAll(
                autoCompleteResources.Select(async autoCompleteResource =>
                {
                    using (SourceCacheContext cacheContext = new SourceCacheContext())
                    {
                        return await autoCompleteResource.VersionStartsWith(packageId, versionPrefix, includePrerelease, cacheContext, logger ?? NullLogger.Instance, cancellationToken);
                    }
                })
            );

            return new SortedSet<NuGetVersion>(
                results.Flatten(),
                VersionComparer.VersionReleaseMetadata
            );
        }
    }
}
