using MSBuildProjectTools.LanguageServer.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Metadata scanner for MSBuild task assemblies.
    /// </summary>
    /// <remarks>
    ///     Used to provide completions for tasks and task parameters.
    /// </remarks>
    public static partial class MSBuildTaskScanner
    {
        /// <summary>
        ///     The name of the MSBuild framework assembly file (Microsoft.Build.Framework.dll).
        /// </summary>
        public static readonly string MSBuildFrameworkAssemblyFileName = "Microsoft.Build.Framework.dll";

        /// <summary>
        ///     The name of the MSBuild tasks assembly file (Microsoft.Build.Tasks.dll).
        /// </summary>
        public static readonly string MSBuildTaskAssemblyFileName = "Microsoft.NET.Build.Tasks.dll";

        /// <summary>
        ///     The fully-qualified name of the MSBuild ITask interface (Microsoft.Build.Framework.ITask).
        /// </summary>
        public static readonly string MSBuildTaskInterfaceType = "Microsoft.Build.Framework.ITask";

        /// <summary>
        ///     The fully-qualified name of the MSBuild [Required] attribute (Microsoft.Build.Framework.RequiredAttribute).
        /// </summary>
        public static readonly string MSBuildRequiredAttributeType = "Microsoft.Build.Framework.RequiredAttribute";

        /// <summary>
        ///     The fully-qualified name of the MSBuild [Output] attribute (Microsoft.Build.Framework.OutputAttribute).
        /// </summary>
        public static readonly string MSBuildOutputAttributeType = "Microsoft.Build.Framework.OutputAttribute";

        /// <summary>
        ///     The full CLR type names of task parameter data-types supported by the scanner.
        /// </summary>
        public static readonly IReadOnlySet<string> SupportedTaskParameterTypes = new HashSet<string>
        {
            typeof(string).FullName,
            typeof(bool).FullName,
            typeof(char).FullName,
            typeof(byte).FullName,
            typeof(short).FullName,
            typeof(int).FullName,
            typeof(long).FullName,
            typeof(float).FullName,
            typeof(double).FullName,
            typeof(DateTime).FullName,
            typeof(Guid).FullName,
            "Microsoft.Build.Framework.ITaskItem",
            "Microsoft.Build.Framework.ITaskItem[]",
            "Microsoft.Build.Framework.ITaskItem2",
            "Microsoft.Build.Framework.ITaskItem2[]"
        };

        /// <summary>
        ///     Scan an assembly for MSBuild tasks.
        /// </summary>
        /// <param name="taskAssemblyFile">
        ///     The task assembly file.
        /// </param>
        /// <param name="targetSdk">
        ///     <see cref="DotnetSdkInfo"/> representing the target .NET SDK.
        /// </param>
        /// <param name="logger">
        ///     An optional logger to use for type-reflection diagnostics.
        /// </param>
        /// <returns>
        ///     <see cref="MSBuildTaskAssemblyMetadata"/> representing the assembly and any MSBuild task definitions that it contains.
        /// </returns>
        public static MSBuildTaskAssemblyMetadata GetAssemblyTaskMetadata(string taskAssemblyFile, DotnetSdkInfo targetSdk, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(taskAssemblyFile))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(taskAssemblyFile)}.", nameof(taskAssemblyFile));

            if (targetSdk == null)
                throw new ArgumentNullException(nameof(targetSdk));

            return GetAssemblyTaskMetadata(taskAssemblyFile, targetSdk.BaseDirectory, logger);
        }

        /// <summary>
        ///     Scan an assembly for MSBuild tasks.
        /// </summary>
        /// <param name="taskAssemblyFile">
        ///     The task assembly file.
        /// </param>
        /// <param name="sdkBaseDirectory">
        ///     The base directory for the target .NET SDK.
        /// </param>
        /// <param name="logger">
        ///     An optional logger to use for type-reflection diagnostics.
        /// </param>
        /// <returns>
        ///     <see cref="MSBuildTaskAssemblyMetadata"/> representing the assembly and any MSBuild task definitions that it contains.
        /// </returns>
        public static MSBuildTaskAssemblyMetadata GetAssemblyTaskMetadata(string taskAssemblyFile, string sdkBaseDirectory, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(taskAssemblyFile))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(taskAssemblyFile)}.", nameof(taskAssemblyFile));

            if (string.IsNullOrWhiteSpace(sdkBaseDirectory))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(sdkBaseDirectory)}.", nameof(sdkBaseDirectory));

            string baseDirectory = Path.GetDirectoryName(taskAssemblyFile);

            using MetadataLoadContext loadContext = new MetadataLoadContext(
                resolver: new SdkAssemblyResolver(
                    baseDirectory: Path.GetDirectoryName(taskAssemblyFile),
                    sdkBaseDirectory,
                    RuntimeEnvironment.GetRuntimeDirectory(),
                    logger
                )
            );

            string msbuildFrameworkAssemblyFile = Path.Combine(sdkBaseDirectory, MSBuildFrameworkAssemblyFileName);
            Assembly msbuildFrameworkAssembly = loadContext.LoadFromAssemblyPath(msbuildFrameworkAssemblyFile);
            if (msbuildFrameworkAssembly == null)
                throw new Exception($"Unable to scan MSBuild task assembly '{taskAssemblyFile}' for tasks (cannot load MSBuild framework from '{msbuildFrameworkAssembly}')."); // TODO: Custom exception type.

            Assembly taskAssembly = loadContext.LoadFromAssemblyPath(taskAssemblyFile);
            if (taskAssembly == null)
                throw new Exception($"An unexpected error occurred while scanning assembly '{taskAssemblyFile}' for tasks."); // TODO: Custom exception type.

            var taskAssemblyMetadata = new MSBuildTaskAssemblyMetadata
            {
                AssemblyName = taskAssembly.FullName,
                AssemblyPath = taskAssembly.Location,
                TimestampUtc = File.GetLastWriteTimeUtc(taskAssembly.Location),
            };

            Type[] taskTypes;

            try
            {
                taskTypes = taskAssembly.GetTypes(); // Even if we can't load everything, try to load what we can.
            }
            catch (ReflectionTypeLoadException typeLoadError)
            {
                taskTypes = typeLoadError.Types;
            }

            taskTypes =
                taskTypes.Where(type =>
                    type != null // Type could not be loaded (see typeLoadError.LoaderExceptions above)
                    &&
                    IsTaskImplementation(type, logger)
                )
                .ToArray();

            foreach (Type taskType in taskTypes)
            {
                var taskMetadata = new MSBuildTaskMetadata
                {
                    Name = taskType.Name,
                    TypeName = taskType.FullName,
                };

                PropertyInfo[] taskProperties = taskType.GetProperties(); // Public properties only.

                foreach (PropertyInfo taskProperty in taskProperties)
                {
                    if (IsTaskParameter(taskProperty, logger, out MSBuildTaskParameterMetadata parameterMetadata))
                        taskMetadata.Parameters.Add(parameterMetadata);
                }

                taskAssemblyMetadata.Tasks.Add(taskMetadata);
            }

            return taskAssemblyMetadata;
        }

        /// <summary>
        ///     Determine whether a CLR <see cref="Type"/> concretely implements an MSBuild task (<see cref="Microsoft.Build.Framework.ITask"/>).
        /// </summary>
        /// <param name="type">
        ///     The target <see cref="Type"/>.
        /// </param>
        /// <param name="logger">
        ///     An optional logger to use for type-reflection diagnostics.
        /// </param>
        /// <returns>
        ///     <c>true</c>
        /// </returns>
        static bool IsTaskImplementation(Type type, ILogger logger)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            try
            {
                if (!type.IsClass)
                    return false;

                if (type.IsAbstract)
                    return false;

                if (type.IsNested)
                    return false;

                return type.GetInterfaces().Any(interfaceType => interfaceType.FullName == MSBuildTaskInterfaceType);
            }
            catch (Exception typeReflectionError)
            {
                if (logger != null)
                {
                    string assemblyQualifiedTypeName;

                    try
                    {
                        assemblyQualifiedTypeName = type.AssemblyQualifiedName;
                    }
                    catch (Exception cannotGetTypeName)
                    {
                        logger.Verbose(cannotGetTypeName, "Unable to determine name for reflected type.");

                        assemblyQualifiedTypeName = "Unknown";
                    }

                    logger.Verbose(typeReflectionError, "Unexpected error while reflecting on type '{AssemblyQualifiedTypeName}'.", assemblyQualifiedTypeName);
                }

                return false;
            }
        }

        /// <summary>
        ///     Determine whether a property represents an MSBuild task parameter.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="logger"></param>
        /// <param name="parameterMetadata"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        static bool IsTaskParameter(PropertyInfo property, ILogger logger, out MSBuildTaskParameterMetadata parameterMetadata)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            parameterMetadata = null;

            string propertyName = property.Name;
            Type declaringType = property.DeclaringType;
            Type propertyType = property.PropertyType;

            try
            {
                MethodInfo propertyAccessor = null;

                if (property.CanRead)
                    propertyAccessor = property.GetGetMethod(nonPublic: false);
                
                if (propertyAccessor == null && property.CanWrite)
                    propertyAccessor = property.GetSetMethod(nonPublic: false);

                if (propertyAccessor == null)
                    return false; // Property can be read / written, but access is not public (which is required for a task-parameter property).

                if (!IsSupportedTaskParameterType(propertyType))
                    return false;

                var attributeTypes = new HashSet<string>(
                    property.GetCustomAttributesData().Select(
                        attributeData => attributeData.AttributeType.FullName
                    )
                );

                parameterMetadata = new MSBuildTaskParameterMetadata
                {
                    Name = property.Name,
                    TypeName = property.PropertyType.FullName,
                    IsRequired = attributeTypes.Contains(MSBuildRequiredAttributeType),
                    IsOutput = attributeTypes.Contains(MSBuildOutputAttributeType)
                };

                if (property.PropertyType.IsEnum)
                {
                    parameterMetadata.EnumMemberNames = new List<string>(
                        Enum.GetNames(property.PropertyType)
                    );
                }

                return true;
            }
            catch (Exception reflectionError)
            {
                if (logger != null)
                    logger.Warning(reflectionError, "Unexpected error while reflecting on property '{PropertyName}' of type '{DeclaringTypeName}'.", propertyName, declaringType.FullName);

                return false;
            }
        }

        /// <summary>
        ///     Determine whether the specified CLR type represents a supported MSBuild task parameter type.
        /// </summary>
        /// <param name="type">
        ///     The <see cref="Type"/> to examine.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the <paramref name="type"/> represents a supported task parameter type; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        ///     Arrays of supported types are also supported, as are enums.
        /// </remarks>
        static bool IsSupportedTaskParameterType(Type type)
        {
            string propertyTypeName = type.FullName;
            if (SupportedTaskParameterTypes.Contains(propertyTypeName))
                return true;

            string arrayTypeName = $"{propertyTypeName}[]";
            if (SupportedTaskParameterTypes.Contains(arrayTypeName))
                return true;

            if (type.IsEnum)
                return true;

            return false;
        }

        /// <summary>
        ///     A <see cref="MetadataAssemblyResolver"/> that uses 
        /// </summary>
        public class SdkAssemblyResolver
            : MetadataAssemblyResolver
        {
            /// <summary>
            ///     Create a new <see cref="SdkAssemblyResolver"/>.
            /// </summary>
            /// <param name="baseDirectory">
            ///     The base directory for task assemblies.
            /// </param>
            /// <param name="sdkBaseDirectory">
            ///     The base directory for the target SDK.
            /// </param>
            /// <param name="runtimeDirectory">
            ///     The base directory for the current runtime environment.
            /// </param>
            /// <param name="logger">
            ///     An optional <see cref="ILogger"/> to use for diagnostic logging.
            /// </param>
            public SdkAssemblyResolver(string baseDirectory, string sdkBaseDirectory, string runtimeDirectory, ILogger logger = null)
            {
                if (string.IsNullOrWhiteSpace(baseDirectory))
                    throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(baseDirectory)}.", nameof(baseDirectory));

                if (string.IsNullOrWhiteSpace(sdkBaseDirectory))
                    throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(sdkBaseDirectory)}.", nameof(sdkBaseDirectory));

                if (string.IsNullOrWhiteSpace(runtimeDirectory))
                    throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(runtimeDirectory)}.", nameof(runtimeDirectory));

                BaseDirectory = baseDirectory;
                SdkBaseDirectory = sdkBaseDirectory;
                RuntimeDirectory = runtimeDirectory;
                Log = (logger ?? Serilog.Log.Logger).ForContext<SdkAssemblyResolver>();
            }

            /// <summary>
            ///     The target SDK's base directory for task assemblies.
            /// </summary>
            public string BaseDirectory { get; }

            /// <summary>
            ///     The base directory for the target SDK.
            /// </summary>
            public string SdkBaseDirectory { get; }

            /// <summary>
            ///     The base directory for the current runtime environment.
            /// </summary>
            public string RuntimeDirectory { get; }

            /// <summary>
            ///     The <see cref="ILogger"/> used for assembly-resolution diagnostic logging.
            /// </summary>
            ILogger Log { get; }

            /// <summary>
            ///     Attempt to find and load the assembly with the specified name.
            /// </summary>
            /// <param name="context">
            ///     Contextual information about the current assembly-resolution process.
            /// </param>
            /// <param name="assemblyName">
            ///     An <see cref="AssemblyName"/> representing the assembly to load.
            /// </param>
            /// <returns>
            ///     The loaded <see cref="Assembly"/>, or <c>null</c> if the assembly could not be found (or loaded).
            /// </returns>
            public override Assembly Resolve(MetadataLoadContext context, AssemblyName assemblyName)
            {
                if (context == null)
                    throw new ArgumentNullException(nameof(context));

                if (assemblyName == null)
                    throw new ArgumentNullException(nameof(assemblyName));

                Log.Verbose($"MSBuild task scanner is attempting to resolve assembly '{assemblyName.Name}'...");

                string foundAssemblyFile = FindAssemblyFile(assemblyName, BaseDirectory);

                if (foundAssemblyFile == null)
                    foundAssemblyFile = FindAssemblyFile(assemblyName, RuntimeDirectory);

                if (foundAssemblyFile == null)
                    foundAssemblyFile = FindAssemblyFile(assemblyName, SdkBaseDirectory);

                if (foundAssemblyFile == null)
                {
                    Log.Verbose($"MSBuild task scanner failed to resolve assembly '{assemblyName.Name}' for metadata.");

                    return null;
                }

                Log.Verbose($"MSBuild task scanner successfully resolved assembly '{assemblyName.Name}' (as '{foundAssemblyFile}') for metadata loader.");

                return context.LoadFromAssemblyPath(foundAssemblyFile);
            }

            /// <summary>
            ///     Attempt to locate the assembly with the specified name.
            /// </summary>
            /// <param name="assemblyName">
            ///     An <see cref="AssemblyName"/> representing the target assembly.
            /// </param>
            /// <param name="baseDirectory">
            ///     The base directory to search for the assembly (subdirectories are included in the search).
            /// </param>
            /// <returns>
            ///     The full path to the assembly file, if found; otherwise, <c>null</c>.
            /// </returns>
            string FindAssemblyFile(AssemblyName assemblyName, string baseDirectory)
            {
                if (assemblyName == null)
                    throw new ArgumentNullException(nameof(assemblyName));

                if (string.IsNullOrWhiteSpace(baseDirectory))
                    throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(baseDirectory)}.", nameof(baseDirectory));

                string foundAssemblyFile = FindAssemblyFile(assemblyName, baseDirectory, extension: "dll");
                if (foundAssemblyFile != null)
                    return foundAssemblyFile;

                // Unusual, but technically it is still supported.
                foundAssemblyFile = FindAssemblyFile(assemblyName, baseDirectory, extension: "exe");
                if (foundAssemblyFile != null)
                    return foundAssemblyFile;

                return null;
            }

            /// <summary>
            ///     Attempt to locate the assembly with the specified name and file extension.
            /// </summary>
            /// <param name="assemblyName">
            ///     An <see cref="AssemblyName"/> representing the target assembly.
            /// </param>
            /// <param name="baseDirectory">
            ///     The base directory to search for the assembly (subdirectories are included in the search).
            /// </param>
            /// <param name="extension">
            ///     The assembly file extension.
            /// </param>
            /// <returns>
            ///     The full path to the assembly file, if found; otherwise, <c>null</c>.
            /// </returns>
            string FindAssemblyFile(AssemblyName assemblyName, string baseDirectory, string extension)
            {
                if (assemblyName == null)
                    throw new ArgumentNullException(nameof(assemblyName));

                if (string.IsNullOrWhiteSpace(baseDirectory))
                    throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(baseDirectory)}.", nameof(baseDirectory));

                if (string.IsNullOrWhiteSpace(extension))
                    throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(extension)}.", nameof(extension));

                string assemblyFileName = $"{assemblyName.Name}.{extension}";

                foreach (string foundAssemblyFile in Directory.EnumerateFiles(baseDirectory, assemblyFileName, SearchOption.AllDirectories))
                {
                    AssemblyName foundAssemblyName;

                    try
                    {
                        foundAssemblyName = AssemblyName.GetAssemblyName(foundAssemblyFile);
                    }
                    catch (Exception invalidAssembly)
                    {
                        Log.Warning(invalidAssembly, "Ignoring assembly file {AssemblyFilePath} (probably not a valid assembly).", foundAssemblyFile);

                        continue;
                    }

                    if (AssemblyName.ReferenceMatchesDefinition(assemblyName, foundAssemblyName))
                    {
                        Log.Verbose($"MSBuild task scanner is using assembly reference ('{foundAssemblyName.FullName}' from '{foundAssemblyFile}') matches definition ('{assemblyName.Name}').");

                        return foundAssemblyFile;
                    }

                    Log.Verbose($"MSBuild task scanner is not using assembly reference ('{foundAssemblyName.FullName}' from '{foundAssemblyFile}') with definition ('{assemblyName.Name}').");
                }

                return null;
            }
        }
    }
}
