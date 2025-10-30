using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace MSBuildProjectTools.LanguageServer.Logging
{
    using LanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;
    using ILanguageServer = OmniSharp.Extensions.LanguageServer.Server.ILanguageServer;

    /// <summary>
    ///     A Serilog logging sink that sends log events to the language server logging facility.
    /// </summary>
    public class LanguageServerLoggingSink
        : ILogEventSink
    {
        /// <summary>
        ///     The language server to which events will be logged.
        /// </summary>
        readonly ILanguageServer _languageServer;

        /// <summary>
        ///     The <see cref="LoggingLevelSwitch"/> that controls logging.
        /// </summary>
        readonly LoggingLevelSwitch _levelSwitch;

        /// <summary>
        ///     Has the language server shut down?
        /// </summary>
        bool _hasServerShutDown;

        /// <summary>
        ///     Create a new language-server event sink.
        /// </summary>
        /// <param name="languageServer">
        ///     The language server to which events will be logged.
        /// </param>
        /// <param name="levelSwitch">
        ///     The <see cref="LoggingLevelSwitch"/> that controls logging.
        /// </param>
        public LanguageServerLoggingSink(ILanguageServer languageServer, LoggingLevelSwitch levelSwitch)
        {
            if (languageServer == null)
                throw new ArgumentNullException(nameof(languageServer));

            if (levelSwitch == null)
                throw new ArgumentNullException(nameof(levelSwitch));

            _languageServer = languageServer;
            _levelSwitch = levelSwitch;

            if (_languageServer is LanguageServer realLanguageServer)
            {
                IDisposable subscription = null;
                subscription = realLanguageServer.Shutdown.Subscribe(shutDownRequested =>
                {
                    subscription?.Dispose();
                    Log.CloseAndFlush();

                    _hasServerShutDown = true;
                });
            }
        }

        /// <summary>
        ///     Can log entries be sent to the language server?
        /// </summary>
        bool CanLog => _languageServer.Window != null && !_hasServerShutDown;

        /// <summary>
        ///     Emit a log event.
        /// </summary>
        /// <param name="logEvent">
        ///     The log event information.
        /// </param>
        public void Emit(LogEvent logEvent)
        {
            if (!CanLog)
                return;

            if (logEvent.Level < _levelSwitch.MinimumLevel)
                return;

            var messageBuilder = new StringBuilder();

            using (var messageWriter = new StringWriter(messageBuilder))
            {
                logEvent.RenderMessage(messageWriter);
            }
            if (logEvent.Exception != null)
            {
                messageBuilder.AppendLine();
                messageBuilder.Append(
                    logEvent.Exception.ToString()
                );
            }

            var logParameters = new LogMessageParams
            {
                Message = messageBuilder.ToString()
            };

            switch (logEvent.Level)
            {
                case LogEventLevel.Error:
                case LogEventLevel.Fatal:
                {
                    logParameters.Type = MessageType.Error;

                    break;
                }
                case LogEventLevel.Warning:
                {
                    logParameters.Type = MessageType.Warning;

                    break;
                }
                case LogEventLevel.Information:
                {
                    logParameters.Type = MessageType.Info;

                    break;
                }
                default:
                {
                    logParameters.Type = MessageType.Log;

                    break;
                }
            }
            _languageServer.Window.LogMessage(logParameters);
        }
    }
}
