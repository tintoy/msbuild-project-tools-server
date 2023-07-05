using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Completion provider for the top-level elements (e.g. PropertyGroup, ItemGroup, Target, etc).
    /// </summary>
    public class TopLevelElementCompletionProvider
        : CompletionProvider
    {
        /// <summary>
        ///     Create a new <see cref="TopLevelElementCompletionProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public TopLevelElementCompletionProvider(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     The provider display name.
        /// </summary>
        public override string Name => "Top-level Elements";

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
        public override async Task<CompletionList> ProvideCompletionsAsync(XmlLocation location, ProjectDocument projectDocument, string triggerCharacters, CancellationToken cancellationToken)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (projectDocument == null)
                throw new ArgumentNullException(nameof(projectDocument));

            List<CompletionItem> completions = new List<CompletionItem>();

            Log.Verbose("Evaluate completions for {XmlLocation:l} (trigger characters = {TriggerCharacters})", location, triggerCharacters);

            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                if (!location.CanCompleteElement(out XSElement replaceElement, parentPath: WellKnownElementPaths.Project))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (not a direct child of the 'Project' element).", location);

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
                HandleTriggerCharacters(triggerCharacters, projectDocument, ref targetRange);

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
        ///     Get top-level element completions.
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

            // <PropertyGroup>
            //     $0
            // </PropertyGroup>
            yield return new CompletionItem
            {
                Label = "<PropertyGroup>",
                Detail = "Element",
                Documentation = MSBuildSchemaHelp.ForElement("PropertyGroup"),
                SortText = Priority + "<PropertyGroup>",
                TextEdit = new TextEdit
                {
                    NewText = "<PropertyGroup>\n\t$0\n</PropertyGroup>",
                    Range = completionRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };

            // <ItemGroup>
            //     $0
            // </ItemGroup>
            yield return new CompletionItem
            {
                Label = "<ItemGroup>",
                Detail = "Element",
                Documentation = MSBuildSchemaHelp.ForElement("ItemGroup"),
                SortText = Priority + "<ItemGroup>",
                TextEdit = new TextEdit
                {
                    NewText = "<ItemGroup>\n\t$0\n</ItemGroup>",
                    Range = completionRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };

            // <Target Name="TargetName">
            //     $0
            // </Target>
            yield return new CompletionItem
            {
                Label = "<Target>",
                Detail = "Element",
                Documentation = MSBuildSchemaHelp.ForElement("Target"),
                SortText = Priority + "<Target>",
                TextEdit = new TextEdit
                {
                    NewText = "<Target Name=\"${1:TargetName}\">\n\t$0\n</Target>",
                    Range = completionRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };

            // <Import Project="ProjectFile" />
            yield return new CompletionItem
            {
                Label = "<Import>",
                Detail = "Element",
                Documentation = MSBuildSchemaHelp.ForElement("Import"),
                SortText = Priority + "<Import>",
                TextEdit = new TextEdit
                {
                    NewText = "<Import Project=\"${1:ProjectFile}\" />$0",
                    Range = completionRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };
        }
    }
}
