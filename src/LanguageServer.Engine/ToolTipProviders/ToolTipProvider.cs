using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.ToolTipProviders
{
    using Documents;
    using SemanticModel;

    /// <summary>
    ///     The base class for tooltip (tooltip) providers.
    /// </summary>
    /// <typeparam name="TDocument">
    ///     The type of <see cref="Document"/> targeted by the tooltip provider.
    /// </typeparam>
    public abstract class ToolTipProvider<TDocument>
        : IToolTipProvider, IToolTipProvider<TDocument>
        where TDocument : Document
    {
        /// <summary>
        ///     Create a new <see cref="ToolTipProvider{TDocument}"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected ToolTipProvider(ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            Log = logger.ForContext(GetType());
        }
        
        /// <summary>
        ///     The sort priority for the provider's tooltip items.
        /// </summary>
        public virtual int Priority => 1000;

        /// <summary>
        ///     The provider logger.
        /// </summary>
        protected ILogger Log { get; }

        /// <summary>
        ///     Provide tooltip content for the specified location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where hovers are requested.
        /// </param>
        /// <param name="document">
        ///     The <see cref="ProjectDocument"/> that contains the <paramref name="location"/>.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="MarkedStringsOrMarkupContent"/>, or <c>null</c> if no tooltips are provided.
        /// </returns>
        public abstract Task<MarkedStringsOrMarkupContent?> ProvideToolTipContentAsync(XmlLocation location, TDocument document, CancellationToken cancellationToken);
    }
}
