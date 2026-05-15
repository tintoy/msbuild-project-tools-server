using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders
{
    using Documents;
    using SemanticModel;

    /// <summary>
    ///     The base class for completion providers.
    /// </summary>
    /// <typeparam name="TDocument">
    ///     The type of <see cref="Document"/> targeted by the completion provider.
    /// </typeparam>
    public abstract class CompletionProvider<TDocument>
        : ICompletionProvider, ICompletionProvider<TDocument>
        where TDocument : Document
    {
        /// <summary>
        ///     Create a new <see cref="CompletionProvider{TDocument}"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected CompletionProvider(ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            Log = logger.ForContext(GetType());
        }

        /// <summary>
        ///     The sort priority for the provider's completion items.
        /// </summary>
        public virtual int Priority => 1000;

        /// <summary>
        ///     The provider logger.
        /// </summary>
        protected ILogger Log { get; }

        /// <summary>
        ///     Provide completions for the specified location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where completions are requested.
        /// </param>
        /// <param name="document">
        ///     The <typeparamref name="TDocument"/> that contains the <paramref name="location"/>.
        /// </param>
        /// <param name="triggerCharacters">
        ///     The character(s), if any, that triggered completion.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="CompletionList"/>, or <c>null</c> if no completions are provided.
        /// </returns>
        public abstract Task<CompletionList> ProvideCompletionsAsync(XmlLocation location, TDocument document, string triggerCharacters, CancellationToken cancellationToken);

        /// <summary>
        ///     Get the textual representation used to sort the completion item with the specified label.
        /// </summary>
        /// <param name="completionLabel">
        ///     The completion item label.
        /// </param>
        /// <param name="priority">
        ///     An optional sort priority (defaults to <see cref="Priority"/>).
        /// </param>
        /// <returns>
        ///     The sort text.
        /// </returns>
        protected virtual string GetItemSortText(string completionLabel, int? priority = null) => $"{priority ?? Priority:0000}{completionLabel}";

        /// <summary>
        ///     Handle characters (if any) that triggered the completion.
        /// </summary>
        /// <param name="document">
        ///     The <see cref="ProjectDocument"/> that contains the <paramref name="targetRange"/>.
        /// </param>
        /// <param name="triggerCharacters">
        ///     The character(s), if any, that triggered completion.
        /// </param>
        /// <param name="targetRange">
        ///     The target <see cref="Range"/> for completions.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if any trigger characters were handled (i.e. the selection was extended); otherwise, <c>false</c>.
        /// </returns>
        protected virtual bool HandleTriggerCharacters(string triggerCharacters, TDocument document, ref Range targetRange) => false;
    }
}
