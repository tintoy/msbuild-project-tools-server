using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders
{
    using Documents;
    using SemanticModel;

    /// <summary>
    ///     Represents a source for completions.
    /// </summary>
    public interface ICompletionProvider
    {
        /// <summary>
        ///     The sort priority for the provider's completion items.
        /// </summary>
        int Priority { get; }
    }

    /// <summary>
    ///     Represents a source for completions.
    /// </summary>
    public interface ICompletionProvider<in TDocument>
        : ICompletionProvider
        where TDocument : Document
    {
        /// <summary>
        ///     Provide completions for the specified location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where completions are requested.
        /// </param>
        /// <param name="document">
        ///     The <see cref="ProjectDocument"/> that contains the <paramref name="location"/>.
        /// </param>
        /// <param name="triggerCharacters">
        ///     The character(s), if any, that triggered completion.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves either a list of <see cref="CompletionItem"/>s, or <c>null</c> if no completions are provided.
        /// </returns>
        Task<CompletionList> ProvideCompletionsAsync(XmlLocation location, TDocument document, string triggerCharacters, CancellationToken cancellationToken);
    }
}
