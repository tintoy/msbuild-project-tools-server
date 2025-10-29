using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using System;

namespace MSBuildProjectTools.LanguageServer.Logging
{
    /// <summary>
    ///     Extension methods for configuring Serilog.
    /// </summary>
    public static class SerilogLanguageServerExtensions
    {
        /// <summary>
        ///     Write log events to the language server logging facility.
        /// </summary>
        /// <param name="loggerSinkConfiguration">
        ///     The logger sink configuration.
        /// </param>
        /// <param name="languageServer">
        ///     The language server to which events will be logged.
        /// </param>
        /// <param name="levelSwitch">
        ///     The <see cref="LoggingLevelSwitch"/> that controls logging.
        /// </param>
        /// <returns>
        ///     The logger configuration.
        /// </returns>
        public static LoggerConfiguration LanguageServer(this LoggerSinkConfiguration loggerSinkConfiguration, ILanguageServer languageServer, LoggingLevelSwitch levelSwitch)
        {
            ArgumentNullException.ThrowIfNull(loggerSinkConfiguration);
            ArgumentNullException.ThrowIfNull(languageServer);
            ArgumentNullException.ThrowIfNull(levelSwitch);

            return loggerSinkConfiguration.Sink(
                new LanguageServerLoggingSink(languageServer, levelSwitch)
            );
        }

        /// <summary>
        ///     Enrich log events with the current logical activity Id (if any).
        /// </summary>
        /// <param name="loggerEnrichmentConfiguration">
        ///     The logger enrichment configuration.
        /// </param>
        /// <returns>
        ///     The logger configuration.
        /// </returns>
        public static LoggerConfiguration WithCurrentActivityId(this LoggerEnrichmentConfiguration loggerEnrichmentConfiguration)
        {
            ArgumentNullException.ThrowIfNull(loggerEnrichmentConfiguration);

            return loggerEnrichmentConfiguration.With<ActivityIdEnricher>();
        }
    }
}
