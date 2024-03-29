using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders
{
    using Documents;
    using SemanticModel;
    using System.Threading;

    /// <summary>
    ///     Represents a source for completions.
    /// </summary>
    public interface ICompletionProvider
    {
        /// <summary>
        ///     Provide completions for the specified location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where completions are requested.
        /// </param>
        /// <param name="projectDocument">
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
        Task<CompletionList> ProvideCompletionsAsync(XmlLocation location, ProjectDocument projectDocument, string triggerCharacters, CancellationToken cancellationToken);
    }
}
