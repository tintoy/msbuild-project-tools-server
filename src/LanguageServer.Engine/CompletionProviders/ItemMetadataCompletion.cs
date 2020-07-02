using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Completion provider for metadata elements / attributes of items.
    /// </summary>
    public class ItemMetadataCompletion
        : CompletionProvider
    {
        /// <summary>
        ///     Create a new <see cref="ItemMetadataCompletion"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public ItemMetadataCompletion(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     The provider display name.
        /// </summary>
        public override string Name => "Item Metadata";

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

            Log.Verbose("Evaluate completions for {XmlLocation:l}", location);

            List<CompletionItem> completions = new List<CompletionItem>();

            using (await projectDocument.Lock.ReaderLockAsync())
            {
                if (location.IsElement(out XSElement element) && !element.HasParentPath(WellKnownElementPaths.ItemGroup))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (not a direct child of an ItemGroup element).", location);

                    return null;
                }

                HashSet<string> existingMetadata = new HashSet<string>();
                
                completions.AddRange(
                    GetAttributeCompletions(location, projectDocument, existingMetadata)
                );

                completions.AddRange(
                    GetElementCompletions(location, projectDocument, triggerCharacters, existingMetadata)
                );
            }

            if (completions.Count == 0)
                return null; // No completions offered

            Log.Verbose("Offering {CompletionCount} completions for {XmlLocation:l}.", location);

            return new CompletionList(completions,
                isIncomplete: false // List is exhaustive
            );
        }

        /// <summary>
        ///     Get completions for item attributes.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where completions are requested.
        /// </param>
        /// <param name="projectDocument">
        ///     The <see cref="ProjectDocument"/> that contains the <paramref name="location"/>.
        /// </param>
        /// <param name="existingMetadata">
        ///     Metadata already declared on the item.
        /// </param>
        /// <returns>
        ///     A sequence of <see cref="CompletionItem"/>s.
        /// </returns>
        IEnumerable<CompletionItem> GetAttributeCompletions(XmlLocation location, ProjectDocument projectDocument, HashSet<string> existingMetadata)
        {
            Log.Verbose("Evaluate attribute completions for {XmlLocation:l}", location);

            XSElement itemElement;
            XSAttribute replaceAttribute;
            PaddingType needsPadding;
            if (!location.CanCompleteAttribute(out itemElement, out replaceAttribute, out needsPadding))
            {
                Log.Verbose("Not offering any attribute completions for {XmlLocation:l} (not a location where we can offer attribute completion.", location);

                yield break;
            }

            // Must be an item element.
            if (!itemElement.HasParentPath(WellKnownElementPaths.ItemGroup))
            {
                Log.Verbose("Not offering any attribute completions for {XmlLocation:l} (element is not a direct child of a 'PropertyGroup' element).", location);

                yield break;
            }

            string itemType = itemElement.Name;
            if (String.IsNullOrWhiteSpace(itemType))
            {
                Log.Verbose("Not offering any attribute completions for {XmlLocation:l} (element represents a new, unnamed, item group).",
                    location,
                    itemType
                );

                yield break;
            }

            if (MSBuildSchemaHelp.ForItemType(itemType) == null)
            {
                Log.Verbose("Not offering any attribute completions for {XmlLocation:l} ({ItemType} is not a well-known item type).",
                    location,
                    itemType
                );

                yield break;
            }

            Log.Verbose("Will offer attribute completions for {XmlLocation:l} (padding: {NeedsPadding})", location, needsPadding);

            // Don't offer completions for existing metadata.
            existingMetadata.UnionWith(
                GetExistingMetadataNames(itemElement)
            );

            Range replaceRange = replaceAttribute?.Range ?? location.Position.ToEmptyRange();

            foreach (string metadataName in MSBuildSchemaHelp.WellKnownItemMetadataNames(itemType))
            {
                if (existingMetadata.Contains(metadataName))
                    continue;

                if (MSBuildHelper.IsWellKnownItemMetadata(metadataName))
                    continue;

                yield return new CompletionItem
                {
                    Label = metadataName,
                    Kind = CompletionItemKind.Field,
                    Detail = "Item Metadata",
                    Documentation = MSBuildSchemaHelp.ForItemMetadata(itemType, metadataName),
                    SortText = GetItemSortText(metadataName),
                    TextEdit = new TextEdit
                    {
                        NewText = $"{metadataName}=\"$0\"".WithPadding(needsPadding),
                        Range = replaceRange.ToLsp()
                    },
                    InsertTextFormat = InsertTextFormat.Snippet
                };
            }
        }

        /// <summary>
        ///     Get completions for item elements.
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
        /// <param name="existingMetadata">
        ///     Metadata already declared on the item.
        /// </param>
        /// <returns>
        ///     A sequence of <see cref="CompletionItem"/>s.
        /// </returns>
        IEnumerable<CompletionItem> GetElementCompletions(XmlLocation location, ProjectDocument projectDocument, string triggerCharacters, HashSet<string> existingMetadata)
        {
            Log.Verbose("Evaluate element completions for {XmlLocation:l}", location);

            XSElement replaceElement;
            if (!location.CanCompleteElement(out replaceElement, parentPath: WellKnownElementPaths.Item))
            {
                Log.Verbose("Not offering any element completions for {XmlLocation:l} (not a location where an item metadata element can be created or replaced by completion).", location);

                yield break;
            }

            Range replaceRange;

            string itemType;
            if (replaceElement != null)
            {
                replaceRange = replaceElement.Range;

                // Replace any characters that were typed to trigger the completion.
                if (triggerCharacters != null)
                    replaceRange = projectDocument.XmlPositions.ExtendLeft(replaceRange, byCharCount: triggerCharacters.Length);

                itemType = replaceElement.ParentElement?.Name;
            }
            else
            {
                replaceRange = location.Position.ToEmptyRange();
                itemType = location.Node.Path.Parent.Name;
            }

            // These items are handled by PackageReferenceCompletion.
            if (itemType == "PackageReference" || itemType == "DotNetCliToolReference")
            {
                Log.Verbose("Not offering any element completions for {XmlLocation:l} ({ItemType} items are handled by another provider).",
                    location,
                    itemType
                );

                yield break;
            }
            
            if (MSBuildSchemaHelp.ForItemType(itemType) == null)
            {
                Log.Verbose("Not offering any element completions for {XmlLocation:l} ({ItemType} is not a well-known item type).",
                    location,
                    itemType
                );

                yield break;
            }

            if (replaceElement != null)
            {
                // Don't offer completions for existing metadata.
                existingMetadata.UnionWith(
                    GetExistingMetadataNames(replaceElement)
                );

                Log.Verbose("Will offer completions to replace item metadata element spanning {Range:l}", replaceRange);
            }
            else
                Log.Verbose("Will offer completions to create item metadata element at {Position:l}", location.Position);

            foreach (string metadataName in MSBuildSchemaHelp.WellKnownItemMetadataNames(itemType))
            {
                if (existingMetadata.Contains(metadataName))
                    continue;

                if (MSBuildHelper.IsWellKnownItemMetadata(metadataName))
                    continue;

                string completionLabel = $"<{metadataName}>";

                yield return new CompletionItem
                {
                    Label = completionLabel,
                    Kind = CompletionItemKind.Field,
                    Detail = $"Item Metadata ({itemType})",
                    Documentation = MSBuildSchemaHelp.ForItemMetadata(itemType, metadataName),
                    SortText = GetItemSortText(completionLabel),
                    TextEdit = new TextEdit
                    {
                        NewText = $"<{metadataName}>$0</{metadataName}>",
                        Range = replaceRange.ToLsp()
                    },
                    InsertTextFormat = InsertTextFormat.Snippet
                };
            }
        }

        /// <summary>
        ///     Get the names of existing metadata on the target item.
        /// </summary>
        /// <param name="itemElement">
        ///     The item element.
        /// </param>
        /// <returns>
        ///     A sequence of existing metadata names.
        /// </returns>
        IEnumerable<string> GetExistingMetadataNames(XSElement itemElement)
        {
            if (itemElement == null)
                throw new ArgumentNullException(nameof(itemElement));
            
            foreach (XSAttribute metadataAttribute in itemElement.Attributes)
                yield return metadataAttribute.Name;

            foreach (XSElement metadataElement in itemElement.ChildElements)
                yield return metadataElement.Name;
        }
    }
}
