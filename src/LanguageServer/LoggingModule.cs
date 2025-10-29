using Autofac;
using Autofac.Core;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer
{
    using Logging;
    using Utilities;
    using Serilog.Core;
    using OmniSharp.Extensions.LanguageServer.Protocol.Server;

    /// <summary>
    ///     Registration logic for logging components.
    /// </summary>
    public class LoggingModule
        : Module
    {
        /// <summary>
        ///     Create a new <see cref="LoggingModule"/>.
        /// </summary>
        public LoggingModule()
        {
        }

        /// <summary>
        ///     Configure logging components.
        /// </summary>
        /// <param name="builder">
        ///     The container builder to configure.
        /// </param>
        protected override void Load(ContainerBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Register(CreateLogger)
                // Create a new LifetimeScope for every new resolve operation,
                // when other resolve operation(s) for this type has already been
                // started but are not completed yet,
                // otherwise use the most nested LifetimeScope in which this resolve
                // operation completed and use its shared instance for the parent
                // resolve operation.
                // In combination with the new behavior of CreateLogger(...) this
                // will behave like a singleton, but can be used in circular dependency
                // situations.
                .LifetimeScopePerInstance(builder)
                .As<ILogger>()
                .OwnedByRootLifetimeScope();
        }

        /// <summary>
        ///     Create the application logger.
        /// </summary>
        /// <param name="componentContext">
        ///     The current component context.
        /// </param>
        /// <param name="parameters">
        ///     Parameters for the current component context.
        /// </param>
        /// <returns>
        ///     The logger.
        /// </returns>
        static ILogger CreateLogger(IComponentContext componentContext, IEnumerable<Parameter> parameters)
        {
            if (componentContext == null)
                throw new ArgumentNullException(nameof(componentContext));

            if (Log.Logger != Logger.None)
                return Log.Logger;

            ILanguageServer languageServer = componentContext.Resolve<ILanguageServer>(parameters);

            Configuration languageServerConfiguration = componentContext.Resolve<Configuration>(parameters);

            // If Logger instance is already set at this point, it was set by resolving ILogger from above dependencies,
            // so return same instance instead of creating new.
            if (Log.Logger != Logger.None)
                return Log.Logger;

            LoggerConfiguration loggerConfiguration = CreateDefaultLoggerConfiguration(languageServerConfiguration)
                .WriteTo.LanguageServer(languageServer, languageServerConfiguration.Logging.LevelSwitch);

            Logger logger = loggerConfiguration.CreateLogger();
            Log.Logger = logger;

            logger.Verbose("Logger initialized.");

            return logger;
        }

        /// <summary>
        ///     Create the default <see cref="LoggerConfiguration"/>, optionally based on language-server configuration.
        /// </summary>
        /// <param name="languageServerConfiguration">
        ///     An optional <see cref="Configuration"/> representing the language-server configuration.
        /// </param>
        /// <returns>
        ///     The new <see cref="LoggerConfiguration"/>.
        /// </returns>
        public static LoggerConfiguration CreateDefaultLoggerConfiguration(Configuration languageServerConfiguration = null)
        {
            languageServerConfiguration ??= new Configuration();

            // Override defaults from environment.
            // We have to use environment variables here since at configuration time there's no LSP connection yet.
            string loggingVerbosityOverride = Environment.GetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_VERBOSE_LOGGING");
            if (loggingVerbosityOverride == "1")
            {
                languageServerConfiguration.Logging.LevelSwitch.MinimumLevel = LogEventLevel.Verbose;
                languageServerConfiguration.Logging.Seq.LevelSwitch.MinimumLevel = LogEventLevel.Verbose;
            }
            string loggingFilePathOverride = Environment.GetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_LOG_FILE");
            if (!string.IsNullOrWhiteSpace(loggingFilePathOverride))
                languageServerConfiguration.Logging.LogFile = loggingFilePathOverride;

            languageServerConfiguration.Logging.Seq.Url = Environment.GetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_SEQ_URL");
            languageServerConfiguration.Logging.Seq.ApiKey = Environment.GetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_SEQ_API_KEY");

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithCurrentActivityId()
                .Enrich.WithDemystifiedStackTraces()
                .Enrich.FromLogContext();

            if (!string.IsNullOrWhiteSpace(languageServerConfiguration.Logging.Seq.Url))
            {
                loggerConfiguration = loggerConfiguration.WriteTo.Seq(languageServerConfiguration.Logging.Seq.Url,
                    apiKey: languageServerConfiguration.Logging.Seq.ApiKey,
                    controlLevelSwitch: languageServerConfiguration.Logging.Seq.LevelSwitch
                );
            }

            if (!string.IsNullOrWhiteSpace(languageServerConfiguration.Logging.LogFile))
            {
                loggerConfiguration = loggerConfiguration.WriteTo.File(
                    path: languageServerConfiguration.Logging.LogFile,
                    levelSwitch: languageServerConfiguration.Logging.LevelSwitch,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}/{Operation}] {Message}{NewLine}{Exception}",
                    flushToDiskInterval: TimeSpan.FromSeconds(1)
                );
            }

            if (Environment.GetEnvironmentVariable("MSBUILD_PROJECT_TOOLS_LOGGING_TO_STDERR") == "1")
            {
                loggerConfiguration = loggerConfiguration.WriteTo.TextWriter(Console.Error,
                    levelSwitch: languageServerConfiguration.Logging.LevelSwitch,
                    outputTemplate: "[{Level}/{Operation}] {Message}{NewLine}{Exception}"
                );
            }

            return loggerConfiguration;
        }
    }
}
