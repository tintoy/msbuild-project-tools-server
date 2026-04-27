using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    /// <summary>
    ///     The base class for integration test suites.
    /// </summary>
    public abstract class IntegrationTestBase
    {
        protected IntegrationTestBase(ITestOutputHelper testOutput)
        {
            ArgumentNullException.ThrowIfNull(testOutput);

            TestOutput = testOutput;

            // Redirect component logging to Serilog.
            Log = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .Enrich.With<LoggingModule.SourceComponentLogEnricher>()
                .Enrich.With<ComputedLogLevelPrefixEnricher>()
                .WriteTo.TestOutput(TestOutput, outputTemplate: "{ComputedLogLevelPrefix:l}{Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        protected ITestOutputHelper TestOutput { get; }

        protected ILogger Log { get; }

        /// <summary>
        ///     Log event enricher that adds a property that remote consumers can use to produce consistent output (when local and remote logs are output to the same stream for display).
        /// </summary>
        class ComputedLogLevelPrefixEnricher
            : ILogEventEnricher
        {
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof(logEvent));

                if (propertyFactory == null)
                    throw new ArgumentNullException(nameof(propertyFactory));

                if (logEvent.MessageTemplate.Text.StartsWith("[SRV"))
                {
                    logEvent.AddOrUpdateProperty(
                        propertyFactory.CreateProperty("ComputedLogLevelPrefix", String.Empty)
                    );
                }
                else
                {
                    string sourceComponent = null;
                    if (logEvent.Properties.TryGetValue("SourceComponent", out LogEventPropertyValue rawSourceComponentProperty) && rawSourceComponentProperty is ScalarValue sourceComponentProperty)
                        sourceComponent = sourceComponentProperty.Value as string;

                    if (String.IsNullOrWhiteSpace(sourceComponent))
                        sourceComponent = "<unknown component>";

                    logEvent.AddOrUpdateProperty(
                        propertyFactory.CreateProperty("ComputedLogLevelPrefix", $"[{logEvent.Level}/{sourceComponent}] ")
                    );
                }
            }
        }
    }
}
