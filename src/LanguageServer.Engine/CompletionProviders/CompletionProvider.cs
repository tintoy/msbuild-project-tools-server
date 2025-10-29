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
    public abstract class CompletionProvider
        : ICompletionProvider
    {
        /// <summary>
        ///     Create a new <see cref="CompletionProvider"/>.
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
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="CompletionList"/>, or <c>null</c> if no completions are provided.
        /// </returns>
        public abstract Task<CompletionList> ProvideCompletionsAsync(XmlLocation location, ProjectDocument projectDocument, string triggerCharacters, CancellationToken cancellationToken);

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
        /// <param name="projectDocument">
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
        protected virtual bool HandleTriggerCharacters(string triggerCharacters, ProjectDocument projectDocument, ref Range targetRange)
        {
            ArgumentNullException.ThrowIfNull(projectDocument);

            // Replace any characters that were typed to trigger the completion.
            if (!String.IsNullOrEmpty(triggerCharacters))
            {
                // The last character typed is implicitly part of the current selection, if it triggered completion.
                int extendSelectionByCharCount = triggerCharacters.Length - 1;
                if (extendSelectionByCharCount > 0)
                {
                    targetRange = projectDocument.XmlPositions.ExtendLeft(targetRange, extendSelectionByCharCount);

                    Log.Verbose("Completion was triggered by typing one or more characters; target range will be extended by {TriggerCharacterCount} characters toward start of document (now: {TargetRange}).", triggerCharacters.Length, targetRange);

                    return true;
                }
            }

            return false;
        }
    }
}
