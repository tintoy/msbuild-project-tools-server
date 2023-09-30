using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System;

namespace MSBuildProjectTools.LanguageServer.Logging
{
    /// <summary>
    ///     Extension methods for configuring and using Serilog.
    /// </summary>
    public static class SerilogLanguageServerExtensions
    {
        /// <summary>
        ///     Attempt to retrieve the log event's source component (i.e. last segment of the "SourceContext" property value).
        /// </summary>
        /// <param name="logEvent">
        ///     The <see cref="LogEvent"/>.
        /// </param>
        /// <param name="sourceComponent">
        ///     Receives the last segment (separator: '.') of the <see cref="LogEvent"/>'s "SourceContext" property value, if present.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the "SourceContext" property is present; otherwise, <c>false</c>.
        /// </returns>
        public static bool TryGetSourceComponent(this LogEvent logEvent, out string sourceComponent)
        {
            if (logEvent == null)
                throw new ArgumentNullException(nameof(logEvent));

            sourceComponent = null;

            string sourceContext;
            if (!logEvent.TryGetSourceContext(out sourceContext))
            {
                sourceContext = "Unknown";

                return false;
            }

            int lastSeparatorIndex = sourceContext.LastIndexOf('.');
            if (lastSeparatorIndex != -1)
                sourceComponent = sourceContext.Substring(lastSeparatorIndex + 1);
            else
                sourceComponent = sourceContext;

            return true;
        }

        /// <summary>
        ///     Attempt to retrieve the log event's "SourceContext" (if present).
        /// </summary>
        /// <param name="logEvent">
        ///     The <see cref="LogEvent"/>.
        /// </param>
        /// <param name="sourceContext">
        ///     Receives the <see cref="LogEvent"/>'s "SourceContext" property value, if present.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the "SourceContext" property is present; otherwise, <c>false</c>.
        /// </returns>
        public static bool TryGetSourceContext(this LogEvent logEvent, out string sourceContext)
        {
            if (logEvent == null)
                throw new ArgumentNullException(nameof(logEvent));

            sourceContext = null;

            LogEventPropertyValue rawPropertyValue;
            if (!logEvent.Properties.TryGetValue("SourceContext", out rawPropertyValue))
                return false;

            ScalarValue scalarPropertyValue = rawPropertyValue as ScalarValue;
            if (scalarPropertyValue == null)
                return false;

            string propertyValue = scalarPropertyValue.Value as string;
            if (String.IsNullOrWhiteSpace(propertyValue))
                return false;

            sourceContext = propertyValue;

            return true;
        }

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
            if (loggerSinkConfiguration == null)
                throw new ArgumentNullException(nameof(loggerSinkConfiguration));

            if (languageServer == null)
                throw new ArgumentNullException(nameof(languageServer));

            if (levelSwitch == null)
                throw new ArgumentNullException(nameof(levelSwitch));

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
            if (loggerEnrichmentConfiguration == null)
                throw new ArgumentNullException(nameof(loggerEnrichmentConfiguration));

            return loggerEnrichmentConfiguration.With<ActivityIdEnricher>();
        }
    }
}
