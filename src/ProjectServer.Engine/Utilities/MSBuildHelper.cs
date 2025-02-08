using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

#pragma warning disable IDE0007 // Use implicit type

namespace MSBuildProjectTools.ProjectServer.Engine.Utilities
{
    using ProjectServer.Utilities;

    /// <summary>
    ///     Helper methods for working with MSBuild projects.
    /// </summary>
    public static class MSBuildHelper
    {
        /// <summary>
        ///     The names of well-known item metadata.
        /// </summary>
        public static readonly ImmutableSortedSet<string> WellknownMetadataNames = [
            "FullPath",
            "RootDir",
            "Filename",
            "Extension",
            "RelativeDir",
            "Directory",
            "RecursiveDir",
            "Identity",
            "ModifiedTime",
            "CreatedTime",
            "AccessedTime"
        ];

        /// <summary>
        ///     The CLR <see cref="Type"/> of this component (<see cref="MSBuildHelper"/>).
        /// </summary>
        static readonly Type ThisComponentType = typeof(MSBuildHelper);

        /// <summary>
        ///     The last .NET SDK version that had a "15.0" sub-folder for "Microsoft.Common.props" (later versions have a "Current" sub-folder instead).
        /// </summary>
        /// <remarks>
        ///     2.1.599 is the theoretical highest version number of the 2.1.5xx feature band, which is the last .NET SDK that ships .NET 2.1 (LTS).
        /// </remarks>
        static readonly SemanticVersion NetCoreLastSdkVersionFor150Folder = new SemanticVersion(major: 2, minor: 1, patch: 599);

        /// <summary>
        ///     A <see cref="VisualStudioInstance"/> representing the currently-registered instance of MSBuild.
        /// </summary>
        static VisualStudioInstance? _registeredMSBuildInstance;

        /// <summary>
        ///     Has a compatible (with the current .NET SDK) version of MSBuild been discovered?
        /// </summary>
        public static bool HaveMSBuild => _registeredMSBuildInstance != null;

        /// <summary>
        ///     The version of MSBuild currently in use (or <c>null</c> if no compatible version of MSBuild has been discovered).
        /// </summary>
        public static Version? MSBuildVersion => _registeredMSBuildInstance?.Version;

        /// <summary>
        ///     The path to the version of MSBuild currently in use (or <c>null</c> if no compatible version of MSBuild has been discovered).
        /// </summary>
        public static string? MSBuildPath => _registeredMSBuildInstance?.MSBuildPath;

        /// <summary>
        ///     Find and use the latest version of the MSBuild engine compatible with the current SDK.
        /// </summary>
        /// <param name="baseDirectory">
        ///     An optional base directory where dotnet.exe should be run (this may affect the version it reports due to global.json).
        /// </param>
        /// <param name="includeNewerRuntimeVersions">
        ///     Include MSBuild instances that target newer versions of the .NET runtime than the current process runtime?
        /// </param>
        /// <param name="enableDotnetHostDiagnostics">
        ///     Enable host-level diagnostics when executing "dotnet" commands?
        /// </param>
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> to use for diagnostic purposes.
        /// </param>
        /// <returns>
        ///     A <see cref="VisualStudioInstance"/> representing the discovered MSBuild engine instance, or <c>null</c> if no appropriate SDK / MSBuild engine was discovered.
        /// </returns>
        public static VisualStudioInstance? DiscoverMSBuildEngine(string? baseDirectory = null, bool includeNewerRuntimeVersions = false, bool enableDotnetHostDiagnostics = false, ILogger? logger = null)
        {
            _registeredMSBuildInstance = null;

            // Assume working directory is VS code's current working directory (i.e. the workspace root).
            //
            // Really, until we figure out a way to change the version of MSBuild we're using after the server has started,
            // we're still going to have problems here.
            //
            // In the end we will probably wind up having to move all the MSBuild stuff out to a separate process, and use something like GRPC (or even Akka.NET's remoting) to communicate with it.
            // It can be stopped and restarted by the language server (even having different instances for different SDK / MSBuild versions).
            //
            // This will also ensure that the language server's model doesn't expose any MSBuild objects anywhere.
            //
            // For now, though, let's choose the dumb option.
            var dotnetInfo = DotnetInfo.GetCurrent(baseDirectory, enableDotnetHostDiagnostics, logger);

            // SDK versions are in SemVer format...
            if (!dotnetInfo.Sdk.HasVersion)
                throw new FormatException($"Cannot determine the version of the .NET SDK at '{dotnetInfo.Sdk.BaseDirectory}'.");

            // ...which MSBuildLocator does not understand.
            var targetSdkVersion = new Version(
                major: dotnetInfo.Sdk.Version.Major,
                minor: dotnetInfo.Sdk.Version.Minor,
                build: dotnetInfo.Sdk.Version.Patch
            );

            List<VisualStudioInstance> allInstances = MSBuildLocator.QueryVisualStudioInstances(new VisualStudioInstanceQueryOptions { AllowAllRuntimeVersions = includeNewerRuntimeVersions }).ToList();
            VisualStudioInstance? latestInstance = allInstances
                .OrderByDescending(instance => instance.Version)
                .FirstOrDefault(instance =>
                    // We need a version of MSBuild for the currently-supported SDK
                    instance.Version == targetSdkVersion
                );
            if (latestInstance == null)
            {
                string foundVersions = string.Join(", ", allInstances.Select(instance => instance.Version));

                throw new Exception($"Cannot locate MSBuild engine for .NET SDK v{targetSdkVersion}. This probably means that MSBuild Project Tools cannot find the MSBuild for the current project instance. It did find the following version(s), though: [{foundVersions}].");
            }

            return latestInstance;
        }

        /// <summary>
        ///     Register an MSBuild engine instance as the MSBuild engine to use.
        /// </summary>
        /// <param name="msbuildInstance"></param>
        /// <remarks>
        ///     This method can, effectively, only be used once per process.
        /// </remarks>
        public static void UseMSBuildEngine(VisualStudioInstance msbuildInstance)
        {
            if (msbuildInstance == null)
                throw new ArgumentNullException(nameof(msbuildInstance));

            if (_registeredMSBuildInstance != null)
            {
                if (msbuildInstance.Version == _registeredMSBuildInstance.Version && String.Equals(msbuildInstance.MSBuildPath, msbuildInstance.MSBuildPath, StringComparison.InvariantCultureIgnoreCase) && String.Equals(msbuildInstance.VisualStudioRootPath, _registeredMSBuildInstance.VisualStudioRootPath, StringComparison.InvariantCultureIgnoreCase))
                    return; // The same MSBuild instance, so nothing to do.

                throw new InvalidOperationException($"Cannot register MSBuild instance '{msbuildInstance.Name}' (v{msbuildInstance.Version}, {msbuildInstance.DiscoveryType}) from '{msbuildInstance.MSBuildPath}' and '{msbuildInstance.VisualStudioRootPath}' because another MSBuild instance has already been registered: '{_registeredMSBuildInstance.Name}' (v{_registeredMSBuildInstance.Version}, {_registeredMSBuildInstance.DiscoveryType}) from '{_registeredMSBuildInstance.MSBuildPath}' and '{_registeredMSBuildInstance.VisualStudioRootPath}'.");
            }

            if (!MSBuildLocator.CanRegister)
                throw new InvalidCastException($"Cannot register MSBuild instance '{msbuildInstance.Name}' (v{msbuildInstance.Version}, {msbuildInstance.DiscoveryType}) from '{msbuildInstance.MSBuildPath}' and '{msbuildInstance.VisualStudioRootPath}' because the MSBuildLocator component indicates that it has already registered an instance of the MSBuild engine.");

            MSBuildLocator.RegisterInstance(msbuildInstance);
            _registeredMSBuildInstance = msbuildInstance;
        }

        /// <summary>
        ///     Create an MSBuild project collection.
        /// </summary>
        /// <param name="solutionDirectory">
        ///     The base (i.e. solution) directory.
        /// </param>
        /// <param name="globalPropertyOverrides">
        ///     An optional dictionary containing property values to override.
        /// </param>
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> to use for diagnostic purposes (if not specified, a dummy logger will be used).
        /// </param>
        /// <returns>
        ///     The project collection.
        /// </returns>
        public static ProjectCollection CreateProjectCollection(string solutionDirectory, Dictionary<string, string>? globalPropertyOverrides = null, ILogger? logger = null)
        {
            logger ??= LogHelper.CreateDummyLogger(ThisComponentType);

            return CreateProjectCollection(solutionDirectory,
                DotnetInfo.GetCurrent(solutionDirectory, logger: logger),
                globalPropertyOverrides
            );
        }

        /// <summary>
        ///     Create an MSBuild project collection.
        /// </summary>
        /// <param name="solutionDirectory">
        ///     The base (i.e. solution) directory.
        /// </param>
        /// <param name="dotnetInfo">
        ///     Information about the current .NET runtime.
        /// </param>
        /// <param name="globalPropertyOverrides">
        ///     An optional dictionary containing property values to override.
        /// </param>
        /// <returns>
        ///     The project collection.
        /// </returns>
        public static ProjectCollection CreateProjectCollection(string solutionDirectory, DotnetInfo dotnetInfo, Dictionary<string, string>? globalPropertyOverrides = null)
        {
            if (string.IsNullOrWhiteSpace(solutionDirectory))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'baseDir'.", nameof(solutionDirectory));

            if (dotnetInfo == null)
                throw new ArgumentNullException(nameof(dotnetInfo));

            if (!dotnetInfo.Sdk.HasBaseDirectory)
                throw new InvalidOperationException("Cannot determine base directory for .NET (check the output of 'dotnet --info').");

            Dictionary<string, string> globalProperties = CreateGlobalMSBuildProperties(dotnetInfo, solutionDirectory, globalPropertyOverrides);
            EnsureMSBuildEnvironment(globalProperties);

            var projectCollection = new ProjectCollection(globalProperties) { IsBuildEnabled = false };

            if (!dotnetInfo.Sdk.HasVersion)
                throw new FormatException($"Cannot determine the version of the .NET SDK at '{dotnetInfo.Sdk.BaseDirectory}'.");

            // Newer versions of the .NET SDK use the toolset version "Current" instead of "15.0" (tintoy/msbuild-project-tools-vscode#46).
            string toolsVersion = dotnetInfo.Sdk.Version <= NetCoreLastSdkVersionFor150Folder ? "15.0" : "Current";

            // Override toolset paths (for some reason these point to the main directory where the dotnet executable lives).
            var toolset = new Toolset(toolsVersion,
                toolsPath: dotnetInfo.Sdk.BaseDirectory,
                projectCollection: projectCollection,
                msbuildOverrideTasksPath: ""
            );

            // Other toolset versions won't be supported by the .NET SDK
            projectCollection.RemoveAllToolsets();

            // TODO: Add configuration setting that enables user to configure custom toolsets.

            projectCollection.AddToolset(toolset);
            projectCollection.DefaultToolsVersion = toolsVersion;

            return projectCollection;
        }

        /// <summary>
        ///     Create global properties for MSBuild.
        /// </summary>
        /// <param name="dotnetInfo">
        ///     Information about the current .NET SDK / runtime.
        /// </param>
        /// <param name="solutionDirectory">
        ///     The base (i.e. solution) directory.
        /// </param>
        /// <param name="globalPropertyOverrides">
        ///     An optional dictionary containing property values to override.
        /// </param>
        /// <returns>
        ///     A dictionary containing the global properties.
        /// </returns>
        public static Dictionary<string, string> CreateGlobalMSBuildProperties(DotnetInfo dotnetInfo, string solutionDirectory, Dictionary<string, string>? globalPropertyOverrides = null)
        {
            if (dotnetInfo == null)
                throw new ArgumentNullException(nameof(dotnetInfo));

            if (!dotnetInfo.Sdk.HasBaseDirectory)
                throw new InvalidOperationException("Cannot determine base directory for .NET (check the output of 'dotnet --info').");

            if (string.IsNullOrWhiteSpace(solutionDirectory))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'solutionDirectory'.", nameof(solutionDirectory));

            if (solutionDirectory.Length > 0 && solutionDirectory[^1] != Path.DirectorySeparatorChar)
                solutionDirectory += Path.DirectorySeparatorChar;

            // Support overriding of SDKs path.
            string? sdksPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
            if (string.IsNullOrWhiteSpace(sdksPath))
                sdksPath = Path.Combine(dotnetInfo.Sdk.BaseDirectory, "Sdks");

            var globalProperties = new Dictionary<string, string>
            {
                [WellKnownPropertyNames.DesignTimeBuild] = "true",
                [WellKnownPropertyNames.BuildProjectReferences] = "false",
                [WellKnownPropertyNames.ResolveReferenceDependencies] = "true",
                [WellKnownPropertyNames.SolutionDir] = solutionDirectory,
                [WellKnownPropertyNames.MSBuildExtensionsPath] = dotnetInfo.Sdk.BaseDirectory,
                [WellKnownPropertyNames.MSBuildSDKsPath] = sdksPath,
                [WellKnownPropertyNames.RoslynTargetsPath] = Path.Combine(dotnetInfo.Sdk.BaseDirectory, "Roslyn")
            };

            if (globalPropertyOverrides != null)
            {
                foreach (string propertyName in globalPropertyOverrides.Keys)
                    globalProperties[propertyName] = globalPropertyOverrides[propertyName];
            }

            return globalProperties;
        }

        /// <summary>
        ///     Ensure that environment variables are populated using the specified MSBuild global properties.
        /// </summary>
        /// <param name="globalMSBuildProperties">
        ///     The MSBuild global properties
        /// </param>
        public static void EnsureMSBuildEnvironment(Dictionary<string, string> globalMSBuildProperties)
        {
            if (globalMSBuildProperties == null)
                throw new ArgumentNullException(nameof(globalMSBuildProperties));

            // Kinda sucks that the simplest way to get MSBuild to resolve SDKs correctly is using environment variables, but there you go.
            Environment.SetEnvironmentVariable(
                WellKnownPropertyNames.MSBuildExtensionsPath,
                globalMSBuildProperties[WellKnownPropertyNames.MSBuildExtensionsPath]
            );
            Environment.SetEnvironmentVariable(
                WellKnownPropertyNames.MSBuildSDKsPath,
                globalMSBuildProperties[WellKnownPropertyNames.MSBuildSDKsPath]
            );
        }

        /// <summary>
        ///     Does the specified property name represent a private property?
        /// </summary>
        /// <param name="propertyName">
        ///     The property name.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the property name starts with an underscore; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsPrivateProperty(string propertyName) => propertyName?.StartsWith("_") ?? false;

        /// <summary>
        ///     Does the specified metadata name represent a private property?
        /// </summary>
        /// <param name="metadataName">
        ///     The metadata name.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the metadata name starts with an underscore; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsPrivateMetadata(string metadataName) => metadataName?.StartsWith("_") ?? false;

        /// <summary>
        ///     Does the specified item type represent a private property?
        /// </summary>
        /// <param name="itemType">
        ///     The item type.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the item type starts with an underscore; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsPrivateItemType(string itemType) => itemType?.StartsWith("_") ?? false;

        /// <summary>
        ///     Determine whether the specified metadata name represents well-known (built-in) item metadata.
        /// </summary>
        /// <param name="metadataName">
        ///     The metadata name.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if <paramref name="metadataName"/> represents well-known item metadata; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsWellKnownItemMetadata(string metadataName) => WellknownMetadataNames.Contains(metadataName);

        /// <summary>
        ///     Create a copy of the project for caching.
        /// </summary>
        /// <param name="project">
        ///     The MSBuild project.
        /// </param>
        /// <returns>
        ///     The project copy (independent of original, but sharing the same <see cref="ProjectCollection"/>).
        /// </returns>
        /// <remarks>
        ///     You can only create a single cached copy for a given project.
        /// </remarks>
        public static Project CloneAsCachedProject(this Project project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            ProjectRootElement clonedXml = project.Xml.DeepClone();
            var clonedProject = new Project(clonedXml, project.GlobalProperties, project.ToolsVersion, project.ProjectCollection)
            {
                FullPath = Path.ChangeExtension(project.FullPath,
                    ".cached" + Path.GetExtension(project.FullPath)
                )
            };

            return clonedProject;
        }

        /// <summary>
        ///     Get the project assets file (usually "project.assets.json").
        /// </summary>
        /// <param name="project">
        ///     The MSBuild <see cref="Project"/>.
        /// </param>
        /// <returns>
        ///     A <see cref="FileInfo"/> representing the project assets file, or <c>null</c> if the project does not have a <see cref="WellKnownPropertyNames.ProjectAssetsFile"/> property.
        /// </returns>
        public static FileInfo? GetProjectAssetsFile(this Project project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            string projectAssetsFile = project.GetPropertyValue(WellKnownPropertyNames.ProjectAssetsFile);
            if (string.IsNullOrWhiteSpace(projectAssetsFile))
                return null;

            if (!Path.IsPathRooted(projectAssetsFile))
                projectAssetsFile = Path.Combine(project.DirectoryPath, projectAssetsFile);

            return new FileInfo(projectAssetsFile);
        }

        /// <summary>
        ///     Asynchronously scan the project's assets file ("project.asset.json") to determine the actual versions of all NuGet packages referenced by the project.
        /// </summary>
        /// <param name="project">
        ///     The MSBuild <see cref="Project"/>.
        /// </param>
        /// <param name="logger">
        ///     An optional <see cref="ILogger"/> to use for diagnostic purposes (if not specified, a dummy logger will be used).
        /// </param>
        /// <returns>
        ///     A dictionary of package semantic versions (keyed by package Id), or <c>null</c> if the project does not have a <see cref="WellKnownPropertyNames.ProjectAssetsFile"/> property (or the project assets file does not exist or has an invalid format).
        /// </returns>
        public static Dictionary<string, SemanticVersion>? GetReferencedPackageVersions(this Project project, ILogger? logger = null)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            logger ??= LogHelper.CreateDummyLogger(ThisComponentType);

            FileInfo? projectAssetsFile = project.GetProjectAssetsFile();
            if (projectAssetsFile == null)
                return null;

            if (!projectAssetsFile.Exists)
                return null;

            JsonNode? rootNode;
            
            try
            {
                using (FileStream projectAssetsContent = projectAssetsFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    rootNode = JsonNode.Parse(projectAssetsContent);
                }

                if (rootNode == null)
                {
                    logger.LogError("Unable to load project assets file {ProjectAssetsFile} (the document is empty).", projectAssetsFile.FullName);

                    return null;
                }
            }
            catch (Exception cannotLoadProjectAssetsJson)
            {
                logger.LogError(cannotLoadProjectAssetsJson, "Unable to load project assets file {ProjectAssetsFile}.", projectAssetsFile.FullName);

                return null;
            }

            JsonNode? libraries = rootNode["libraries"];
            if (libraries == null)
            {
                logger.LogWarning("Project assets file {ProjectAssetsFile} has invalid format (missing 'libraries' property on root object).", projectAssetsFile.FullName);

                return null;
            }

            var referencedPackageVersions = new Dictionary<string, SemanticVersion>(StringComparer.OrdinalIgnoreCase);

            foreach (var (libraryName, _) in libraries.AsObject())
            {
                // Property names should be in the format "libName/semVer".
                string[] nameComponents = libraryName.Split(
                    separator: '/',
                    count: 2
                );

                if (nameComponents.Length != 2)
                    continue; // Invalid format.

                string name = nameComponents[0];
                if (!SemanticVersion.TryParse(nameComponents[1], out SemanticVersion version))
                    continue; // Not a valid semantic version.

                referencedPackageVersions[name] = version;
            }

            return referencedPackageVersions;
        }

        /// <summary>
        ///     The names of well-known MSBuild properties.
        /// </summary>
        public static class WellKnownPropertyNames
        {
            /// <summary>
            ///     The "MSBuildExtensionsPath" property.
            /// </summary>
            public static readonly string MSBuildExtensionsPath = "MSBuildExtensionsPath";

            /// <summary>
            ///     The "MSBuildExtensionsPath32" property.
            /// </summary>
            public static readonly string MSBuildExtensionsPath32 = "MSBuildExtensionsPath32";

            /// <summary>
            ///     The "MSBuildSDKsPath" property.
            /// </summary>
            public static readonly string MSBuildSDKsPath = "MSBuildSDKsPath";

            /// <summary>
            ///     The "MSBuildToolsPath" property.
            /// </summary>
            public static readonly string MSBuildToolsPath = "MSBuildToolsPath";

            /// <summary>
            ///     The "SolutionDir" property.
            /// </summary>
            public static readonly string SolutionDir = "SolutionDir";

            /// <summary>
            ///     The "_ResolveReferenceDependencies" property.
            /// </summary>
            public static readonly string ResolveReferenceDependencies = "_ResolveReferenceDependencies";

            /// <summary>
            ///     The "DesignTimeBuild" property.
            /// </summary>
            public static readonly string DesignTimeBuild = "DesignTimeBuild";

            /// <summary>
            ///     The "BuildProjectReferences" property.
            /// </summary>
            public static readonly string BuildProjectReferences = "BuildProjectReferences";

            /// <summary>
            ///     The "RoslynTargetsPath" property.
            /// </summary>
            public static readonly string RoslynTargetsPath = "RoslynTargetsPath";

            /// <summary>
            ///     The "ProjectAssetsFile" property.
            /// </summary>
            public static readonly string ProjectAssetsFile = "ProjectAssetsFile";
        }
    }
}
