using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using Serilog;
using System;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    using CustomProtocol;

    /// <summary>
    ///     Language Server message handler that tracks configuration.
    /// </summary>
    public sealed class ConfigurationHandler
        : IDidChangeConfigurationSettingsHandler
    {
        /// <summary>
        ///     Create a new <see cref="ConfigurationHandler"/>.
        /// </summary>
        /// <param name="configuration">
        ///     The language server configuration.
        /// </param>
        public ConfigurationHandler(Configuration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            Configuration = configuration;
        }

        /// <summary>
        ///     Raised when configuration has changed.
        /// </summary>
        public event EventHandler<EventArgs> ConfigurationChanged;

        /// <summary>
        ///     The language server configuration.
        /// </summary>
        public Configuration Configuration { get; }

        /// <summary>
        ///     Called when configuration has changed.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        Task OnDidChangeConfiguration(DidChangeConfigurationObjectParams parameters)
        {
            Configuration.UpdateFrom(parameters);

            ConfigurationChanged?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Called to inform the handler of the language server's configuration capabilities.
        /// </summary>
        /// <param name="capabilities">
        ///     A <see cref="SynchronizationCapability"/> data structure representing the capabilities.
        /// </param>
        void ICapability<DidChangeConfigurationCapability>.SetCapability(DidChangeConfigurationCapability capabilities)
        {
        }

        /// <summary>
        ///     Handle a change in configuration.
        /// </summary>
        /// <param name="parameters">
        ///     The notification parameters.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task INotificationHandler<DidChangeConfigurationObjectParams>.Handle(DidChangeConfigurationObjectParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            using (BeginOperation("OnDidChangeConfiguration"))
            {
                try
                {
                    await OnDidChangeConfiguration(parameters);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnDidChangeConfiguration");
                }
            }
        }

        /// <summary>
        ///     Unused.
        /// </summary>
        /// <returns>
        ///     <c>null</c>
        /// </returns>
        object IRegistration<object>.GetRegistrationOptions()
        {
            return null;
        }

        /// <summary>
        ///     Add log context for an operation.
        /// </summary>
        /// <param name="operationName">
        ///     The operation name.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the log-context scope.
        /// </returns>
        static IDisposable BeginOperation(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'operationName'.", nameof(operationName));

            return Serilog.Context.LogContext.PushProperty("Operation", operationName);
        }
    }
}
