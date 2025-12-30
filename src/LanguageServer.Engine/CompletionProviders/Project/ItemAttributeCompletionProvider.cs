using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders.Project
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Completion provider for attributes of items.
    /// </summary>
    public class ItemAttributeCompletionProvider
        : CompletionProvider<ProjectDocument>
    {
        /// <summary>
        ///     The names of well-known attributes for MSBuild item elements.
        /// </summary>
        public static readonly ImmutableHashSet<string> WellKnownItemAttributes =
            ImmutableHashSet.CreateRange(new string[]
            {
                "Include",
                "Condition",
                "Exclude",
                "Update"
            });

        /// <summary>
        ///     Create a new <see cref="ItemAttributeCompletionProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public ItemAttributeCompletionProvider(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     The sort priority for the provider's completions.
        /// </summary>
        public override int Priority => 500;

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
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="CompletionList"/>s, or <c>null</c> if no completions are provided.
        /// </returns>
        public override async Task<CompletionList> ProvideCompletionsAsync(XmlLocation location, ProjectDocument document, string triggerCharacters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(location);

            ArgumentNullException.ThrowIfNull(document);

            var completions = new List<CompletionItem>();

            using (await document.Lock.ReaderLockAsync(cancellationToken))
            {
                if (!location.CanCompleteAttribute(out XSElement element, out XSAttribute replaceAttribute, out PaddingType needsPadding))
                    return null;

                // Must be a valid item element.
                if (!element.IsValid || !element.HasParentPath(WellKnownElementPaths.ItemGroup))
                    return null;

                Range replaceRange = replaceAttribute?.Range ?? location.Position.ToEmptyRange();

                completions.AddRange(
                    WellKnownItemAttributes.Except(
                        element.AttributeNames
                    )
                    .Select(attributeName => new CompletionItem
                    {
                        Label = attributeName,
                        Detail = "Attribute",
                        Documentation =
                            MSBuildSchemaHelp.ForItemMetadata(itemType: element.Name, metadataName: attributeName)
                            ??
                            MSBuildSchemaHelp.ForAttribute(element.Name, attributeName),
                        Kind = CompletionItemKind.Field,
                        SortText = GetItemSortText(attributeName),
                        TextEdit = new TextEdit
                        {
                            NewText = $"{attributeName}=\"$1\"$0".WithPadding(needsPadding),
                            Range = replaceRange.ToLsp()
                        },
                        InsertTextFormat = InsertTextFormat.Snippet
                    })
                );
            }

            if (completions.Count == 0)
                return null;

            return new CompletionList(completions, isIncomplete: false);
        }
    }
}
