using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.ToolTipProviders
{
    using Documents;
    using SemanticModel;

    /// <summary>
    ///     Represents a source for tool-tips.
    /// </summary>
    public interface IToolTipProvider
    {
        /// <summary>
        ///     The sort priority for the provider's hover items.
        /// </summary>
        int Priority { get; }
    }

    /// <summary>
    ///     Represents a source for hovers.
    /// </summary>
    public interface IToolTipProvider<in TDocument>
        : IToolTipProvider
        where TDocument : Document
    {
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
        Task<MarkedStringsOrMarkupContent?> ProvideToolTipContentAsync(XmlLocation location, TDocument document, CancellationToken cancellationToken);
    }
}
