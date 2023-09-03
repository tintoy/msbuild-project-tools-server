using Newtonsoft.Json;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;

// TODO: Update Configuration to read from flattened settings schema.

namespace MSBuildProjectTools.LanguageServer
{
    /// <summary>
    ///     The configuration for the MSBuild language service.
    /// </summary>
    public sealed class Configuration
    {
        /// <summary>
        ///     The name of the configuration section as passed in messages such as <see cref="CustomProtocol.DidChangeConfigurationObjectParams"/>.
        /// </summary>
        public static readonly string SectionName = "msbuildProjectTools";

        /// <summary>
        ///     The MSBuild language service's logging configuration.
        /// </summary>
        [JsonProperty("logging", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public LoggingConfiguration Logging { get; } = new LoggingConfiguration();

        /// <summary>
        ///     The MSBuild language service's main configuration.
        /// </summary>
        [JsonProperty("language", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public LanguageConfiguration Language { get; } = new LanguageConfiguration();

        /// <summary>
        ///     The MSBuild language service's MSBuild engine configuration.
        /// </summary>
        [JsonProperty("msbuild", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public MSBuildConfiguration MSBuild { get; } = new MSBuildConfiguration();

        /// <summary>
        ///     The MSBuild language service's NuGet configuration.
        /// </summary>
        [JsonProperty("nuget", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public NuGetConfiguration NuGet { get; } = new NuGetConfiguration();

        /// <summary>
        ///     Experimental features (if any) that are currently enabled.
        /// </summary>
        [JsonProperty("experimentalFeatures", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public HashSet<string> EnableExperimentalFeatures { get; } = new HashSet<string>();
    }

    /// <summary>
    ///     Logging settings for the MSBuild language service.
    /// </summary>
    public class LoggingConfiguration
    {
        /// <summary>
        ///     The minimum log level for regular logging.
        /// </summary>
        [JsonProperty("level")]
        public LogEventLevel Level { get => LevelSwitch.MinimumLevel; set => LevelSwitch.MinimumLevel = value; }

        /// <summary>
        ///     The serilog log-level switch for regular logging.
        /// </summary>
        [JsonIgnore]
        public LoggingLevelSwitch LevelSwitch { get; } = new LoggingLevelSwitch(LogEventLevel.Information);

        /// <summary>
        ///     The name of the file (if any) to which log entries are written.
        /// </summary>
        /// <remarks>
        ///     Included here only for completeness; the client supplies this setting via environment variable.
        /// </remarks>
        [JsonProperty("file")]
        public string LogFile { get; set; }

        /// <summary>
        ///     The MSBuild language service's Seq logging configuration.
        /// </summary>
        [JsonProperty("seq", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public SeqLoggingConfiguration Seq { get; } = new SeqLoggingConfiguration();

        /// <summary>
        ///     Enable verbose tracing of LSP messages between client and server?
        /// </summary>
        [JsonProperty("trace")]
        public bool Trace { get; set; }

        /// <summary>
        ///     Is the minimum logging level Debug or Verbose?
        /// </summary>
        [JsonIgnore]
        public bool IsDebugLoggingEnabled => LevelSwitch.MinimumLevel <= LogEventLevel.Debug;
    }

    /// <summary>
    ///     Seq-related logging configuration for the language service.
    /// </summary>
    public class SeqLoggingConfiguration
    {
        /// <summary>
        ///     The minimum log level for Seq logging.
        /// </summary>
        [JsonProperty("level")]
        public LogEventLevel Level { get => LevelSwitch.MinimumLevel; set => LevelSwitch.MinimumLevel = value; }

        /// <summary>
        ///     The serilog log-level switch for logging to Seq.
        /// </summary>
        [JsonIgnore]
        public LoggingLevelSwitch LevelSwitch { get; } = new LoggingLevelSwitch(LogEventLevel.Verbose);

        /// <summary>
        ///     The URL of the Seq server (or <c>null</c> to disable logging).
        /// </summary>
        /// <remarks>
        ///     Included here only for completeness; the client supplies this setting via environment variable.
        /// </remarks>
        [JsonProperty("url")]
        public string Url { get; set; }

        /// <summary>
        ///     An optional API key used to authenticate to Seq.
        /// </summary>
        /// <remarks>
        ///     Included here only for completeness; the client supplies this setting via environment variable.
        /// </remarks>
        [JsonProperty("apiKey")]
        public string ApiKey { get; set; }
    }

    /// <summary>
    ///     The main settings for the MSBuild language service.
    /// </summary>
    public class LanguageConfiguration
    {
        /// <summary>
        ///     Language service features (if any) to disable.
        /// </summary>
        [JsonProperty("disable", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public DisabledFeatureConfiguration DisableFeature { get; } = new DisabledFeatureConfiguration();

        /// <summary>
        ///     Types of object from the current project to include when offering completions.
        /// </summary>
        [JsonProperty("completionsFromProject", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public HashSet<CompletionSource> CompletionsFromProject { get; } = new HashSet<CompletionSource>();
    }

    /// <summary>
    ///     Configuration for the MSBuild engine.
    /// </summary>
    public class MSBuildConfiguration
    {
        /// <summary>
        ///     Override the default value of MSBuildExtensionsPath.
        /// </summary>
        [JsonProperty("extensionsPath")]
        public string ExtensionsPath { get; set; }

        /// <summary>
        ///     Override the default value of MSBuildExtensionsPath32.
        /// </summary>
        [JsonProperty("extensionsPath32")]
        public string ExtensionsPath32 { get; set; }
    }

    /// <summary>
    ///     Configuration for disabled language-service features.
    /// </summary>
    public class DisabledFeatureConfiguration
    {
        /// <summary>
        ///     Disable tooltips when hovering on XML in MSBuild project files?
        /// </summary>
        [JsonProperty("hover")]
        public bool Hover { get; set; }
    }

    /// <summary>
    ///     NuGet-related configuration for the language service.
    /// </summary>
    public class NuGetConfiguration
    {
        /// <summary>
        ///     Disable automatic warm-up of the NuGet API client?
        /// </summary>
        [JsonProperty("disablePreFetch")]
        public bool DisablePreFetch { get; set; } = false;

        /// <summary>
        ///     Include suggestions for pre-release packages and package versions?
        /// </summary>
        [JsonProperty("includePreRelease", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool IncludePreRelease { get; set; } = false;

        /// <summary>
        ///     Include suggestions for packages from local (file-based) package sources?
        /// </summary>
        [JsonProperty("includeLocalSources", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool IncludeLocalSources { get; set; } = false;

        /// <summary>
        /// The names/URIs of configured NuGet package sources that should be ignored (i.e. not be used) by the language server.
        /// </summary>
        [JsonProperty("ignorePackageSources", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public HashSet<string> IgnorePackageSources { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Sort package versions in descending order (i.e. newest versions first)?
        /// </summary>
        [JsonProperty("newestVersionsFirst")]
        public bool ShowNewestVersionsFirst { get; set; } = true;
    }

    /// <summary>
    ///     Represents a data-source for completion.
    /// </summary>
    public enum CompletionSource
    {
        /// <summary>
        ///     Item types.
        /// </summary>
        ItemType,

        /// <summary>
        ///     Item metadata names.
        /// </summary>
        ItemMetadata,

        /// <summary>
        ///     Property names.
        /// </summary>
        Property,

        /// <summary>
        ///     Target names.
        /// </summary>
        Target,

        /// <summary>
        ///     Task metadata (names, parameters, etc).
        /// </summary>
        Task
    }
}
