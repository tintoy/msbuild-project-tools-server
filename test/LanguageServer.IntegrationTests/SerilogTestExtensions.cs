using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using System.IO;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    /// <summary>
    ///     Extension methods for Serilog.
    /// </summary>
    public static class SerilogTestExtensions
    {
        /// <summary>
        ///     The default format for messages written to test output.
        /// </summary>
        const string DefaultOutputTemplate = "[{Level:u3}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        ///     Write to XUnit test output.
        /// </summary>
        /// <param name="loggerConfiguration">
        ///     The logger configuration.
        /// </param>
        /// <param name="testOutput">
        ///     The XUnit test output helper.
        /// </param>
        /// <param name="levelSwitch">
        ///     An optional logging-level switch.
        /// </param>
        /// <param name="outputTemplate">
        ///     An optional message template for log output.
        /// </param>
        /// <returns>
        ///     The configured <see cref="LoggerConfiguration"/>.
        /// </returns>
        public static LoggerConfiguration TestOutput(this Serilog.Configuration.LoggerSinkConfiguration loggerConfiguration, ITestOutputHelper testOutput, LoggingLevelSwitch levelSwitch = null, string outputTemplate = DefaultOutputTemplate)
        {
            return loggerConfiguration.Sink(
                new TestOutputSink(testOutput, outputTemplate),
                levelSwitch: levelSwitch
            );
        }

        /// <summary>
        ///     A Serilog sink that writes to XUnit test output.
        /// </summary>
        class TestOutputSink
            : Serilog.Core.ILogEventSink
        {
            /// <summary>
            ///     The XUnit test output helper.
            /// </summary>
            readonly ITestOutputHelper _testOutput;

            /// <summary>
            ///     The message formatter.
            /// </summary>
            readonly MessageTemplateTextFormatter _formatter;

            /// <summary>
            ///     Create a new <see cref="TestOutputSink"/>.
            /// </summary>
            /// <param name="testOutput">
            ///     The XUnit test output helper.
            /// </param>
            /// <param name="outputTemplate">
            ///     The message template for log output.
            /// </param>
            public TestOutputSink(ITestOutputHelper testOutput, string outputTemplate)
            {
                _testOutput = testOutput;
                _formatter = new MessageTemplateTextFormatter(outputTemplate);
            }

            /// <summary>
            ///     Emit a log event to the sink.
            /// </summary>
            /// <param name="logEvent">
            ///     The log event to emit.
            /// </param>
            public void Emit(LogEvent logEvent)
            {
                using (var buffer = new StringWriter())
                {
                    _formatter.Format(logEvent, buffer);
                    _testOutput.WriteLine(
                        buffer.ToString().TrimEnd()
                    );
                }
            }
        }
    }
}
