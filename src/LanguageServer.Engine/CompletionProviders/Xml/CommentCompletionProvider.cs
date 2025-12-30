using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders.Xml
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Completion provider for XML comments.
    /// </summary>
    public class CommentCompletionProvider
        : CompletionProvider<XmlDocument>
    {
        /// <summary>
        ///     Create a new <see cref="CommentCompletionProvider"/> provider.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public CommentCompletionProvider(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     Provide completions for the specified location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where completions are requested.
        /// </param>
        /// <param name="document">
        ///     The <see cref="XmlDocument"/> that contains the <paramref name="location"/>.
        /// </param>
        /// <param name="triggerCharacters">
        ///     The character(s), if any, that triggered completion.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="CompletionList"/>s, or <c>null</c> if no completions are provided.
        /// </returns>
        public override async Task<CompletionList> ProvideCompletionsAsync(XmlLocation location, XmlDocument document, string triggerCharacters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(location);

            ArgumentNullException.ThrowIfNull(document);

            var completions = new List<CompletionItem>();

            Log.Verbose("Evaluate completions for {XmlLocation:l}", location);

            using (await document.Lock.ReaderLockAsync(cancellationToken))
            {
                if (!location.CanCompleteElement(out XSElement replaceElement))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (cannot insert or replace an element here).", location);

                    return null;
                }

                Range targetRange;

                if (replaceElement != null)
                {
                    targetRange = replaceElement.Range;

                    Log.Verbose("Offering completions to replace element {ElementName} @ {ReplaceRange:l}",
                        replaceElement.Name,
                        targetRange
                    );
                }
                else
                {
                    targetRange = location.Position.ToEmptyRange();

                    Log.Verbose("Offering completions to insert element @ {InsertPosition:l}",
                        location.Position
                    );
                }

                // Replace any characters that were typed to trigger the completion.
                HandleTriggerCharacters(triggerCharacters, document, ref targetRange);

                completions.AddRange(
                    GetCompletionItems(targetRange)
                );
            }

            Log.Verbose("Offering {CompletionCount} completion(s) for {XmlLocation:l}", completions.Count, location);

            if (completions.Count == 0)
                return null;

            return new CompletionList(completions,
                isIncomplete: false // Consider this list to be exhaustive
            );
        }

        /// <summary>
        ///     Get comment completions.
        /// </summary>
        /// <param name="replaceRange">
        ///     The range of text to be replaced by the completions.
        /// </param>
        /// <returns>
        ///     A sequence of <see cref="CompletionItem"/>s.
        /// </returns>
        public IEnumerable<CompletionItem> GetCompletionItems(Range replaceRange)
        {
            LspModels.Range completionRange = replaceRange.ToLsp();

            // <!--  -->
            yield return new CompletionItem
            {
                Label = "<!-- -->",
                Detail = "Comment",
                Documentation = "XML comment",
                SortText = Priority + "<!-- -->",
                TextEdit = new TextEdit
                {
                    NewText = "<!-- $0 -->",
                    Range = completionRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };
        }
    }
}
