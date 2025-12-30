using Microsoft.Build.Evaluation;
using Microsoft.Language.Xml;
using Nito.AsyncEx;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer.Documents
{
    using SemanticModel;
    using SemanticModel.MSBuildExpressions;
    using Utilities;

    /// <summary>
    ///     Represents the document state for an MSBuild project.
    /// </summary>
    public abstract class ProjectDocument
        : XmlDocument
    {
        /// <summary>
        ///     The project's configured package sources.
        /// </summary>
        readonly List<PackageSource> _configuredPackageSources = new List<PackageSource>();

        /// <summary>
        ///     The project's referenced package versions, keyed by package Id.
        /// </summary>
        readonly Dictionary<string, SemanticVersion> _referencedPackageVersions = new Dictionary<string, SemanticVersion>();

        /// <summary>
        ///     NuGet auto-complete APIs for configured package sources.
        /// </summary>
        readonly List<AutoCompleteResource> _autoCompleteResources = new List<AutoCompleteResource>();

        /// <summary>
        ///     The underlying MSBuild project collection.
        /// </summary>
        public ProjectCollection MSBuildProjectCollection { get; protected set; }

        /// <summary>
        ///     The underlying MSBuild project.
        /// </summary>
        public Project MSBuildProject { get; protected set; }

        /// <summary>
        ///     Is the underlying MSBuild project cached (i.e. out-of-date with respect to the source text)?
        /// </summary>
        /// <remarks>
        ///     If the current project XML is invalid, the original MSBuild project is retained, but <see cref="MSBuildLocator"/> functionality will be unavailable (since source positions may no longer match up).
        /// </remarks>
        public bool IsMSBuildProjectCached { get; private set; }

        /// <summary>
        ///     Is parsing of MSBuild expressions enabled?
        /// </summary>
        public bool EnableExpressions { get; set; }

        /// <summary>
        ///     Create a new <see cref="ProjectDocument"/>.
        /// </summary>
        /// <param name="workspace">
        ///     The document workspace.
        /// </param>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected ProjectDocument(Workspace workspace, DocumentUri documentUri, ILogger logger)
            : base(workspace, documentUri, logger)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(documentUri);

            if (ProjectFile.Extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
                ProjectKind = ProjectKind.Project;
            else if (ProjectFile.Extension.Equals(".props", StringComparison.OrdinalIgnoreCase))
                ProjectKind = ProjectKind.Properties;
            else if (ProjectFile.Extension.Equals(".targets", StringComparison.OrdinalIgnoreCase))
                ProjectKind = ProjectKind.Targets;
            else
                ProjectKind = ProjectKind.Other;
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="ProjectDocument"/>.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected override void Dispose(bool disposing)
        {
        }

        /// <summary>
        ///     The document kind.
        /// </summary>
        public override DocumentKind DocumentKind => DocumentKind.Project;

        /// <summary>
        ///     The project file.
        /// </summary>
        public FileInfo ProjectFile => DocumentFile;

        /// <summary>
        ///     The kind of project.
        /// </summary>
        public ProjectKind ProjectKind { get; }

        /// <summary>
        ///     Is the underlying MSBuild project currently loaded?
        /// </summary>
        public bool HasMSBuildProject => HasXml && MSBuildProjectCollection != null && MSBuildProject != null;

        /// <summary>
        ///     The project MSBuild object-lookup facility.
        /// </summary>
        protected MSBuildObjectLocator MSBuildLocator { get; private set; }

        /// <summary>
        ///     MSBuild objects in the project that correspond to locations in the file.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     The project is cached or not loaded.
        /// </exception>
        public IEnumerable<MSBuildObject> MSBuildObjects
        {
            get
            {
                if (!HasMSBuildProject)
                    throw new InvalidOperationException($"MSBuild project '{ProjectFile.FullName}' is not loaded.");

                if (IsMSBuildProjectCached)
                    throw new InvalidOperationException($"MSBuild project '{ProjectFile.FullName}' is a cached (out-of-date) copy because the project XML is currently invalid; positional lookups can't work in this scenario.");

                return MSBuildLocator.AllObjects;
            }
        }

        /// <summary>
        ///     NuGet package sources configured for the current project.
        /// </summary>
        public IReadOnlyList<PackageSource> ConfiguredPackageSources => _configuredPackageSources;

        /// <summary>
        ///     The project's referenced package versions, keyed by package Id.
        /// </summary>
        public IReadOnlyDictionary<string, SemanticVersion> ReferencedPackageVersions => _referencedPackageVersions;

        /// <summary>
        ///     Load and parse the project.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task representing the load operation.
        /// </returns>
        public override async ValueTask Load(CancellationToken cancellationToken = default)
        {
            await base.Load(cancellationToken);

            if (!HasXml)
                return;

            await ConfigurePackageSources(cancellationToken);

            bool loaded = TryLoadMSBuildProject();
            if (loaded)
                MSBuildLocator = new MSBuildObjectLocator(MSBuildProject, XmlLocator, XmlPositions);
            else
                MSBuildLocator = null;

            IsMSBuildProjectCached = !loaded;

            UpdatePackageReferences();
        }

        /// <summary>
        ///     Update the project in-memory state.
        /// </summary>
        /// <param name="xml">
        ///     The project XML.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task representing the update operation.
        /// </returns>
        public override async ValueTask Update(string xml, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(xml);

            await base.Update(xml, cancellationToken);

            if (!HasXml)
                return; 

            bool loaded = TryLoadMSBuildProject();
            if (loaded)
                MSBuildLocator = new MSBuildObjectLocator(MSBuildProject, XmlLocator, XmlPositions);
            else
                MSBuildLocator = null;

            IsMSBuildProjectCached = !loaded;

            UpdatePackageReferences();
        }

        /// <summary>
        ///     Unload the project.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        public override async ValueTask Unload(CancellationToken cancellationToken = default)
        {
            await base.Unload(cancellationToken);

            TryUnloadMSBuildProject();
            MSBuildLocator = null;
            IsMSBuildProjectCached = false;

            Xml = null;
            XmlPositions = null;
            IsDirty = false;
        }

        /// <summary>
        ///     Determine the NuGet package sources configured for the current project and create clients for them.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the package sources were loaded; otherwise, <c>false</c>.
        /// </returns>
        public virtual async Task<bool> ConfigurePackageSources(CancellationToken cancellationToken = default)
        {
            try
            {
                _configuredPackageSources.Clear();
                _autoCompleteResources.Clear();

                bool includeLocalSources = Workspace.Configuration.NuGet.IncludeLocalSources;
                HashSet<string> ignoredPackageSources = Workspace.Configuration.NuGet.IgnorePackageSources;

                foreach (PackageSource packageSource in NuGetHelper.GetWorkspacePackageSources(ProjectFile.Directory.FullName))
                {
                    // Exclude package sources explicitly ignored by name.
                    string packageSourceName = packageSource.Name ?? "<unknown>";
                    if (ignoredPackageSources.Contains(packageSourceName))
                    {
                        Log.Verbose("Ignoring package source named {PackageSourceName} (the language server has been explicitly configured to ignore it).", packageSourceName);

                        continue;
                    }

                    // Exclude package sources explicitly ignored by URI.
                    Uri packageSourceUri = packageSource.TrySourceAsUri ?? new Uri("unknown:/", UriKind.Absolute);
                    if (ignoredPackageSources.Contains(packageSourceUri.AbsoluteUri))
                    {
                        Log.Verbose("Ignoring package source with URI {PackageSourceURI} (the language server has been explicitly configured to ignore it).", packageSourceUri.AbsoluteUri);

                        continue;
                    }

                    // Exclude unsupported package-source types.
                    if (!packageSource.IsHttp)
                    {
                        if (packageSourceUri.Scheme == Uri.UriSchemeFile)
                        {
                            if (!includeLocalSources)
                            {
                                Log.Verbose("Ignoring local package source {PackageSourceName} ({PackageSourcePath}) (the language server has not been configured to use local package sources).",
                                    packageSourceName,
                                    packageSourceUri.AbsolutePath
                                );

                                continue;
                            }
                        }
                        else
                        {
                            Log.Verbose("Ignoring local package source {PackageSourceName} ({PackageSourceUri}) (the language server only supports local and HTTP-based package sources).",
                                packageSourceName,
                                packageSourceUri.AbsolutePath
                            );

                            continue;
                        }
                    }

                    _configuredPackageSources.Add(packageSource);
                }

                Log.Information("{PackageSourceCount} package sources configured for project {ProjectFile}.",
                    _configuredPackageSources.Count,
                    DocumentUri.GetFileSystemPath(DocumentUri)
                );
                foreach (PackageSource packageSource in _configuredPackageSources)
                {
                    if (packageSource.IsMachineWide)
                    {
                        Log.Information("  Globally-configured package source {PackageSourceName} (v{PackageSourceProtocolVersion}) => {PackageSourceUri}",
                            packageSource.Name,
                            packageSource.ProtocolVersion,
                            packageSource.SourceUri
                        );
                    }
                    else
                    {
                        Log.Information("  Locally-configured package source {PackageSourceName} (v{PackageSourceProtocolVersion}) => {PackageSourceUri}",
                            packageSource.Name,
                            packageSource.ProtocolVersion,
                            packageSource.SourceUri
                        );
                    }
                }

                List<SourceRepository> sourceRepositories = _configuredPackageSources.CreateResourceRepositories();
                foreach (SourceRepository sourceRepository in sourceRepositories)
                {
                    ServiceIndexResourceV3 serviceIndex = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);
                    if (serviceIndex == null)
                    {
                        Log.Warning("    Ignoring configured package source {PackageSourceName} ({PackageSourceUri}) because the v3 service index cannot be found for this package source.",
                            sourceRepository.PackageSource.Name ?? "<unknown>",
                            sourceRepository.PackageSource.TrySourceAsUri?.AbsoluteUri ?? "unknown:/"
                        );

                        continue;
                    }

                    IReadOnlyList<ServiceIndexEntry> autoCompleteServices = serviceIndex.GetServiceEntries(ServiceTypes.SearchAutocompleteService);
                    if (autoCompleteServices.Count == 0)
                    {
                        Log.Warning("    Ignoring configured package source {PackageSourceName} ({PackageSourceUri}) because it does not appear to support a compatible version of the NuGet auto-complete API.",
                            sourceRepository.PackageSource.Name ?? "<unknown>",
                            sourceRepository.PackageSource.TrySourceAsUri?.AbsoluteUri ?? "unknown:/"
                        );

                        continue;
                    }

                    AutoCompleteResource autoCompleteResource = await sourceRepository.GetResourceAsync<AutoCompleteResource>(cancellationToken);
                    if (autoCompleteResource == null)
                    {
                        // Should not happen.
                        Log.Error("Failed to retrieve {ServiceName} service instance for configured package source {PackageSourceName} ({PackageSourceUri}).",
                            "AutoComplete",
                            sourceRepository.PackageSource.Name ?? "<unknown>",
                            sourceRepository.PackageSource.TrySourceAsUri?.AbsoluteUri ?? "unknown:/"
                        );

                        continue;
                    }

                    _autoCompleteResources.Add(autoCompleteResource);
                }

                return true;
            }
            catch (Exception packageSourceLoadError)
            {
                Log.Error(packageSourceLoadError, "Error configuring NuGet package sources for MSBuild project '{ProjectFileName}'.", ProjectFile.FullName);

                return false;
            }
        }

        /// <summary>
        ///     Re-scan referenced packages for the current project.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if the package references were successfully scanned and updated; otherwise, <c>false</c>.
        /// </returns>
        public virtual bool UpdatePackageReferences()
        {
            try
            {
                _referencedPackageVersions.Clear();

                if (!HasMSBuildProject)
                {
                    Log.Debug("Not scanning package references (although existing references have been cleared) for MSBuild project {ProjectFileName} because the project is not currently loaded.", ProjectFile.FullName);

                    return false;
                }

                FileInfo projectAssetsFile = MSBuildProject.GetProjectAssetsFile();
                if (projectAssetsFile == null)
                {
                    Log.Debug("Not scanning package references for project {ProjectFileName} because it does not define a project assets file (Property:{PropertyName}).",
                        ProjectFile.FullName,
                        MSBuildHelper.WellKnownPropertyNames.ProjectAssetsFile
                    );

                    return false;
                }
                if (!projectAssetsFile.Exists)
                {
                    Log.Debug("Not scanning package references for project {ProjectFileName} because its project assets file ({ProjectAssetsFileName}) was not found.",
                        ProjectFile.FullName,
                        MSBuildHelper.WellKnownPropertyNames.ProjectAssetsFile
                    );

                    return false;
                }

                Dictionary<string, SemanticVersion> referencedPackageVersions = MSBuildProject.GetReferencedPackageVersions();
                if (referencedPackageVersions == null)
                    return false;

                _referencedPackageVersions.AddRange(referencedPackageVersions);

                return true;
            }
            catch (Exception packageReferenceUpdateError)
            {
                Log.Error(packageReferenceUpdateError, "Error scanning NuGet package references for MSBuild project '{ProjectFileName}'.", ProjectFile.FullName);

                return false;
            }
        }

        /// <summary>
        ///     Suggest package Ids based on the specified package Id prefix.
        /// </summary>
        /// <param name="packageIdPrefix">
        ///     The package Id prefix.
        /// </param>
        /// <param name="includePrerelease">
        ///     Include packages for which only pre-release versions are available?
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task that resolves to a sorted set of suggested package Ids.
        /// </returns>
        public virtual async Task<SortedSet<string>> SuggestPackageIds(string packageIdPrefix, bool includePrerelease, CancellationToken cancellationToken = default)
        {
            // We don't actually need a working MSBuild project for this, but we do want parsable XML.
            if (!HasXml)
                throw new InvalidOperationException($"XML for project '{ProjectFile.FullName}' is not loaded.");

            Log.Debug("Requesting suggestions from {PackageSourceCount} package source(s) for NuGet package Ids matching prefix {PackageIdPrefix} (include pre-release: {IncludePreRelease})...",
                _autoCompleteResources.Count,
                packageIdPrefix,
                includePrerelease
            );

            SortedSet<string> packageIds = await _autoCompleteResources.SuggestPackageIds(packageIdPrefix, includePrerelease, cancellationToken: cancellationToken);

            Log.Debug("Found {PackageIdSuggestionCount} suggestions from {PackageSourceCount} package source(s) for NuGet package Ids matching prefix {PackageIdPrefix} (include pre-release: {IncludePreRelease}).",
                _autoCompleteResources.Count,
                packageIds.Count,
                packageIdPrefix,
                includePrerelease
            );

            return packageIds;
        }

        /// <summary>
        ///     Suggest package versions for the specified package.
        /// </summary>
        /// <param name="packageId">
        ///     The package Id.
        /// </param>
        /// <param name="includePrerelease">
        ///     Include pre-release package versions?
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task that resolves to a sorted set of suggested package versions.
        /// </returns>
        public virtual async Task<SortedSet<NuGetVersion>> SuggestPackageVersions(string packageId, bool includePrerelease, CancellationToken cancellationToken = default)
        {
            // We don't actually need a working MSBuild project for this, but we do want parsable XML.
            if (!HasXml)
                throw new InvalidOperationException($"XML for project '{ProjectFile.FullName}' is not loaded.");

            Log.Debug("Requesting suggestions for NuGet package versions matching Id {PackageId} (include pre-release: {IncludePreRelease})...",
                packageId,
                includePrerelease
            );

            SortedSet<NuGetVersion> packageVersions = await _autoCompleteResources.SuggestPackageVersions(packageId, includePrerelease, cancellationToken: cancellationToken);

            Log.Debug("Found {PackageVersionSuggestionCount} suggestions for NuGet package versions matching Id {PackageId} (include pre-release: {IncludePreRelease}).",
                packageVersions.Count,
                packageId,
                includePrerelease
            );

            return packageVersions;
        }

        /// <summary>
        ///     Warm up the project's NuGet client.
        /// </summary>
        protected virtual void WarmUpNuGetClient()
        {
            SuggestPackageIds("Newtonsoft.Json", includePrerelease: false).ContinueWith(task =>
            {
                foreach (Exception exception in task.Exception.Flatten().InnerExceptions)
                {
                    Log.Debug(exception,
                        "Error initializing NuGet client. {ErrorMessage}",
                        exception.Message
                    );
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        ///     Get the XML object (if any) at the specified position in the project file.
        /// </summary>
        /// <param name="position">
        ///     The target position.
        /// </param>
        /// <returns>
        ///     The object, or <c>null</c> if no object was found at the specified position.
        /// </returns>
        public SyntaxNode GetXmlAtPosition(Position position)
        {
            if (!HasXml)
                throw new InvalidOperationException($"XML for project '{ProjectFile.FullName}' is not loaded.");

            return Xml.FindNode(position, XmlPositions);
        }

        /// <summary>
        ///     Get the XML object (if any) at the specified position in the project file.
        /// </summary>
        /// <typeparam name="TXml">
        ///     The type of XML object to return.
        /// </typeparam>
        /// <param name="position">
        ///     The target position.
        /// </param>
        /// <returns>
        ///     The object, or <c>null</c> no object of the specified type was found at the specified position.
        /// </returns>
        public TXml GetXmlAtPosition<TXml>(Position position)
            where TXml : SyntaxNode
        {
            return GetXmlAtPosition(position) as TXml;
        }

        /// <summary>
        ///     Get the MSBuild object (if any) at the specified position in the project file.
        /// </summary>
        /// <param name="position">
        ///     The target position.
        /// </param>
        /// <returns>
        ///     The MSBuild object, or <c>null</c> no object was found at the specified position.
        /// </returns>
        public MSBuildObject GetMSBuildObjectAtPosition(Position position)
        {
            if (!HasMSBuildProject)
                throw new InvalidOperationException($"MSBuild project '{ProjectFile.FullName}' is not loaded.");

            if (IsMSBuildProjectCached)
                throw new InvalidOperationException($"MSBuild project '{ProjectFile.FullName}' is a cached (out-of-date) copy because the project XML is currently invalid; positional lookups can't work in this scenario.");

            return MSBuildLocator.Find(position);
        }

        /// <summary>
        ///     Get the expression's containing range.
        /// </summary>
        /// <param name="expression">
        ///     The MSBuild expression.
        /// </param>
        /// <param name="relativeTo">
        ///     The range of the <see cref="XSNode"/> that contains the expression.
        /// </param>
        /// <returns>
        ///     The containing <see cref="Range"/>.
        /// </returns>
        public Range GetRange(ExpressionNode expression, Range relativeTo)
        {
            ArgumentNullException.ThrowIfNull(expression);

            return GetRange(expression, relativeTo.Start);
        }

        /// <summary>
        ///     Get the expression's containing range.
        /// </summary>
        /// <param name="expression">
        ///     The MSBuild expression.
        /// </param>
        /// <param name="relativeToPosition">
        ///     The starting position of the <see cref="XSNode"/> that contains the expression.
        /// </param>
        /// <returns>
        ///     The containing <see cref="Range"/>.
        /// </returns>
        public Range GetRange(ExpressionNode expression, Position relativeToPosition)
        {
            ArgumentNullException.ThrowIfNull(expression);

            if (!HasXml)
                throw new InvalidOperationException($"XML for project '{ProjectFile.FullName}' is not loaded.");

            int absoluteBasePosition = XmlPositions.GetAbsolutePosition(relativeToPosition);

            return XmlPositions.GetRange(
                absoluteBasePosition + expression.AbsoluteStart,
                absoluteBasePosition + expression.AbsoluteEnd
            );
        }

        /// <summary>
        ///     Retrieve metadata for all tasks defined in the project.
        /// </summary>
        /// <returns>
        ///     A dictionary of task assembly metadata, keyed by assembly path.
        /// </returns>
        /// <remarks>
        ///     Cache metadata (and persist cache to file).
        /// </remarks>
        public List<MSBuildTaskAssemblyMetadata> GetMSBuildProjectTaskAssemblies()
        {
            if (!HasMSBuildProject)
                throw new InvalidOperationException($"MSBuild project '{ProjectFile.FullName}' is not loaded.");

            var currentRuntime = DotnetInfo.GetCurrent();

            var taskAssemblyFiles = new List<string>
            {
                // Include "built-in" tasks.
                Path.Combine(currentRuntime.BaseDirectory, "Microsoft.Build.Tasks.Core.dll"),
                Path.Combine(currentRuntime.BaseDirectory, "Roslyn", "Microsoft.Build.Tasks.CodeAnalysis.dll")
            };

            taskAssemblyFiles.AddRange(
                MSBuildProject.GetAllUsingTasks()
                    .Where(usingTask => !string.IsNullOrWhiteSpace(usingTask.AssemblyFile))
                    .Distinct(UsingTaskAssemblyEqualityComparer.Instance) // Ensure each assembly path is only evaluated once.
                    .Select(usingTask => Path.GetFullPath(
                        Path.Combine(
                            usingTask.ContainingProject.DirectoryPath,
                            MSBuildProject.ExpandString(usingTask.AssemblyFile)
                                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                        )
                    ))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            );

            var metadata = new List<MSBuildTaskAssemblyMetadata>();
            foreach (string taskAssemblyFile in taskAssemblyFiles)
            {
                Log.Verbose("Scanning assembly {TaskAssemblyFile} for task metadata...", taskAssemblyFile);

                if (!File.Exists(taskAssemblyFile))
                {
                    Log.Information("Skipped scan of task metadata for assembly {TaskAssemblyFile} (file not found).", taskAssemblyFile);

                    continue;
                }

                MSBuildTaskAssemblyMetadata assemblyMetadata = Workspace.TaskMetadataCache.GetAssemblyMetadata(taskAssemblyFile, currentRuntime.Sdk.BaseDirectory);
                metadata.Add(assemblyMetadata);

                Log.Verbose("Completed scanning of assembly {TaskAssemblyFile} for task metadata ({DiscoveredTaskCount} tasks discovered).", taskAssemblyFile, assemblyMetadata.Tasks.Count);
            }

            // Persist any changes to cached metadata.
            Workspace.PersistTaskMetadataCache();

            return metadata;
        }

        /// <summary>
        ///     Get overrides (if any) for MSBuild global properties.
        /// </summary>
        protected virtual Dictionary<string, string> GetMSBuildGlobalPropertyOverrides()
        {
            var propertyOverrides = new Dictionary<string, string>(Workspace.Configuration.MSBuild.GlobalProperties, Workspace.Configuration.MSBuild.GlobalProperties.Comparer);
            if (!string.IsNullOrWhiteSpace(Workspace.Configuration.MSBuild.ExtensionsPath))
                propertyOverrides[MSBuildHelper.WellKnownPropertyNames.MSBuildExtensionsPath] = Workspace.Configuration.MSBuild.ExtensionsPath;
            if (!string.IsNullOrWhiteSpace(Workspace.Configuration.MSBuild.ExtensionsPath32))
                propertyOverrides[MSBuildHelper.WellKnownPropertyNames.MSBuildExtensionsPath32] = Workspace.Configuration.MSBuild.ExtensionsPath32;

            return propertyOverrides;
        }

        /// <summary>
        ///     Attempt to load the underlying MSBuild project.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if the project was successfully loaded; otherwise, <c>false</c>.
        /// </returns>
        protected abstract bool TryLoadMSBuildProject();

        /// <summary>
        ///     Attempt to unload the underlying MSBuild project.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if the project was successfully unloaded; otherwise, <c>false</c>.
        /// </returns>
        protected abstract bool TryUnloadMSBuildProject();

    }
}
