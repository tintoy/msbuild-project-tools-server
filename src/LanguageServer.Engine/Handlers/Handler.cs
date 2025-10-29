using OmniSharp.Extensions.JsonRpc;
using Serilog;
using System;
using System.Reactive.Disposables;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using MSBuildProjectTools.LanguageServer.Utilities;

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    /// <summary>
    ///     The base class for language server event handlers.
    /// </summary>
    public abstract class Handler
        : IJsonRpcHandler
    {
        /// <summary>
        ///     Create a new <see cref="Handler"/>.
        /// </summary>
        /// <param name="server">
        ///     The language server.
        /// </param>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected Handler(ILanguageServer server, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(server);

            Server = server;
            Log = logger.ForContext(GetType());
        }

        /// <summary>
        ///     The handler's logger.
        /// </summary>
        protected ILogger Log { get; }

        /// <summary>
        ///     The language server.
        /// </summary>
        protected ILanguageServer Server { get; }

        /// <summary>
        ///     Add an activity / log-context scope for an operation.
        /// </summary>
        /// <param name="operationName">
        ///     The operation name.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the log-context scope.
        /// </returns>
        protected static IDisposable BeginOperation(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'operationName'.", nameof(operationName));

            return new CompositeDisposable(
                ActivityCorrelationManager.BeginActivityScope(),
                Serilog.Context.LogContext.PushProperty("Operation", operationName)
            );
        }
    }
}
