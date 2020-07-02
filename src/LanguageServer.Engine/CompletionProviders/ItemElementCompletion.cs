using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Completion provider for the common item elements.
    /// </summary>
    public class ItemElementCompletion
        : CompletionProvider
    {
        /// <summary>
        ///     Create a new <see cref="ItemElementCompletion"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public ItemElementCompletion(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     The provider display name.
        /// </summary>
        public override string Name => "Item Elements";

        /// <summary>
        ///     The provider's default priority for completion items.
        /// </summary>
        public override int Priority => 1000;

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
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="CompletionList"/>s, or <c>null</c> if no completions are provided.
        /// </returns>
        public override async Task<CompletionList> ProvideCompletions(XmlLocation location, ProjectDocument projectDocument, string triggerCharacters, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (projectDocument == null)
                throw new ArgumentNullException(nameof(projectDocument));

            List<CompletionItem> completions = new List<CompletionItem>();

            Log.Verbose("Evaluate completions for {XmlLocation:l} (trigger characters = {TriggerCharacters})", location, triggerCharacters);

            using (await projectDocument.Lock.ReaderLockAsync())
            {
                XSElement replaceElement;
                if (!location.CanCompleteElement(out replaceElement, parentPath: WellKnownElementPaths.ItemGroup))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (not a direct child of a 'ItemGroup' element).", location);

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

                    Log.Verbose("Offering completions to create element @ {ReplaceRange:l}",
                        targetRange
                    );
                }

                // Replace any characters that were typed to trigger the completion.
                if (triggerCharacters != null)
                {
                    targetRange = projectDocument.XmlPositions.ExtendLeft(targetRange, byCharCount: triggerCharacters.Length);

                    Log.Verbose("Completion was triggered by typing one or more characters; target range will be extended by {TriggerCharacterCount} characters toward start of document (now: {TargetRange}).", triggerCharacters.Length, targetRange);
                }

                completions.AddRange(
                    GetCompletionItems(projectDocument, targetRange)
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
        ///     Get item element completions.
        /// </summary>
        /// <param name="projectDocument">
        ///     The <see cref="ProjectDocument"/> for which completions will be offered.
        /// </param>
        /// <param name="replaceRange">
        ///     The range of text to be replaced by the completions.
        /// </param>
        /// <returns>
        ///     A sequence of <see cref="CompletionItem"/>s.
        /// </returns>
        public IEnumerable<CompletionItem> GetCompletionItems(ProjectDocument projectDocument, Range replaceRange)
        {
            if (replaceRange == null)
                throw new ArgumentNullException(nameof(replaceRange));

            LspModels.Range replaceRangeLsp = replaceRange.ToLsp();

            HashSet<string> offeredItemNames = new HashSet<string>
            {
                "PackageReference",
                "DotNetCliToolReference"
            };

            // Well-known (but standard-format) properties.

            foreach (string wellKnownItemName in MSBuildSchemaHelp.WellKnownItemTypes)
            {
                if (!offeredItemNames.Add(wellKnownItemName))
                    continue;

                yield return ItemCompletion(wellKnownItemName, replaceRangeLsp,
                    description: MSBuildSchemaHelp.ForItemType(wellKnownItemName)
                );
            }
            
            if (!projectDocument.HasMSBuildProject)
                yield break; // Without a valid MSBuild project (even a cached one will do), we can't inspect existing MSBuild properties.

            if (!projectDocument.Workspace.Configuration.Language.CompletionsFromProject.Contains(CompletionSource.ItemType))
                yield break;

            int otherItemPriority = Priority + 10;

            string[] otherItemNames =
                projectDocument.MSBuildProject.Properties
                    .Select(item => item.Name)
                    .Where(itemName => !itemName.StartsWith("_")) // Ignore private item types.
                    .ToArray();
            foreach (string itemName in otherItemNames)
            {
                if (!offeredItemNames.Add(itemName))
                    continue;

                yield return ItemCompletion(itemName, replaceRangeLsp, otherItemPriority,
                    description: $"I don't know anything about the '{itemName}' item type, but it's defined in this project (or a project that it imports); you can override its value by specifying it here."
                );
            }
        }

        /// <summary>
        ///     Create a standard <see cref="CompletionItem"/> for the specified MSBuild item.
        /// </summary>
        /// <param name="itemName">
        ///     The MSBuild item name.
        /// </param>
        /// <param name="replaceRange">
        ///     The range of text that will be replaced by the completion.
        /// </param>
        /// <param name="priority">
        ///     The item sort priority (defaults to <see cref="CompletionProvider.Priority"/>).
        /// </param>
        /// <param name="description">
        ///     An optional description for the item.
        /// </param>
        /// <returns>
        ///     The <see cref="CompletionItem"/>.
        /// </returns>
        CompletionItem ItemCompletion(string itemName, LspModels.Range replaceRange, int? priority = null, string description = null)
        {
            return new CompletionItem
            {
                Label = $"<{itemName}>",
                Detail = "Item",
                Documentation = description,
                Kind = CompletionItemKind.Class,
                SortText = $"{priority ?? Priority:0000}<{itemName}>",
                TextEdit = new TextEdit
                {
                    NewText = $"<{itemName} Include=\"$1\" />$0",
                    Range = replaceRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };
        }
    }
}
