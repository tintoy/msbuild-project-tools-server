using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.IO;

using ILogger = Serilog.ILogger;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents cached data for MSBuild task assemblies.
    /// </summary>
    public sealed class MSBuildTaskMetadataCache
    {
        /// <summary>
        ///     Settings for serialization of cache state.
        /// </summary>
        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        /// <summary>
        ///     Create a new <see cref="MSBuildTaskMetadataCache"/>.
        /// </summary>
        /// <param name="logger">
        ///     An optional logger to use for type-reflection diagnostics.
        /// </param>
        public MSBuildTaskMetadataCache(ILogger logger)
        {
            Log = logger;
        }

        /// <summary>
        ///     An optional logger to use for type-reflection diagnostics.
        /// </summary>
        ILogger Log { get; }

        /// <summary>
        ///     A lock used to synchronize access to cache state.
        /// </summary>
        [JsonIgnore]
        public AsyncLock StateLock { get; } = new AsyncLock();

        /// <summary>
        ///     Has the cache been modified since it was last persisted?
        /// </summary>
        [JsonIgnore]
        public bool IsDirty { get; set; }

        /// <summary>
        ///     Metadata for assemblies, keyed by the assembly's full path.
        /// </summary>
        [JsonProperty("assemblies", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public Dictionary<string, MSBuildTaskAssemblyMetadata> Assemblies = new Dictionary<string, MSBuildTaskAssemblyMetadata>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Get metadata for the specified task assembly, updating the cache if required.
        /// </summary>
        /// <param name="assemblyPath">
        ///     The full path to the assembly.
        /// </param>
        /// <param name="sdkBaseDirectory">
        ///     The base directory for the target .NET SDK.
        /// </param>
        /// <returns>
        ///     The assembly metadata.
        /// </returns>
        public MSBuildTaskAssemblyMetadata GetAssemblyMetadata(string assemblyPath, string sdkBaseDirectory)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'assemblyPath'.", nameof(assemblyPath));

            if (string.IsNullOrWhiteSpace(sdkBaseDirectory))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(sdkBaseDirectory)}.", nameof(sdkBaseDirectory));

            MSBuildTaskAssemblyMetadata metadata;
            using (StateLock.Lock())
            {
                var assemblyFile = new FileInfo(assemblyPath);
                if (!Assemblies.TryGetValue(assemblyPath, out metadata) || metadata.TimestampUtc < assemblyFile.LastWriteTimeUtc)
                {
                    metadata = MSBuildTaskScanner.GetAssemblyTaskMetadata(assemblyPath, sdkBaseDirectory,
                        logger: Log?.ForContext(
                            typeof(MSBuildTaskScanner)
                        )
                    );
                    Assemblies[metadata.AssemblyPath] = metadata;

                    IsDirty = true;
                }
            }

            return metadata;
        }

        /// <summary>
        ///     Flush the cache.
        /// </summary>
        public void Flush()
        {
            using (StateLock.Lock())
            {
                Assemblies.Clear();

                IsDirty = true;
            }
        }

        /// <summary>
        ///     Load cache state from the specified file.
        /// </summary>
        /// <param name="cacheFile">
        ///     The file containing persisted cache state.
        /// </param>
        public void Load(string cacheFile)
        {
            if (string.IsNullOrWhiteSpace(cacheFile))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'cacheFile'.", nameof(cacheFile));

            using (StateLock.Lock())
            {
                Assemblies.Clear();

                using (StreamReader input = File.OpenText(cacheFile))
                using (var json = new JsonTextReader(input))
                {
                    JsonSerializer.Create(SerializerSettings).Populate(json, this);
                }

                IsDirty = false;
            }
        }

        /// <summary>
        ///     Write cache state to the specified file.
        /// </summary>
        /// <param name="cacheFile">
        ///     The file that will contain cache state.
        /// </param>
        public void Save(string cacheFile)
        {
            if (string.IsNullOrWhiteSpace(cacheFile))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'cacheFile'.", nameof(cacheFile));

            using (StateLock.Lock())
            {
                if (File.Exists(cacheFile))
                    File.Delete(cacheFile);

                using (StreamWriter output = File.CreateText(cacheFile))
                using (var json = new JsonTextWriter(output))
                {
                    JsonSerializer.Create(SerializerSettings).Serialize(json, this);
                }

                IsDirty = false;
            }
        }

        /// <summary>
        ///     Create a <see cref="MSBuildTaskMetadataCache"/> using the state persisted in the specified file.
        /// </summary>
        /// <param name="cacheFile">
        ///     The file containing persisted cache state.
        /// </param>
        /// <param name="logger">
        ///     An optional logger to use for type-reflection diagnostics.
        /// </param>
        /// <returns>
        ///     The new <see cref="MSBuildTaskMetadataCache"/>.
        /// </returns>
        public static MSBuildTaskMetadataCache FromCacheFile(string cacheFile, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(cacheFile))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'cacheFile'.", nameof(cacheFile));

            var cache = new MSBuildTaskMetadataCache(logger);
            cache.Load(cacheFile);

            return cache;
        }
    }
}
