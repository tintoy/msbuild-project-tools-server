using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using NuGet.Versioning;
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
    ///     Completion provider for "PackageReference" and "DotNetCliToolReference" items.
    /// </summary>
    public class PackageReferenceCompletion
        : CompletionProvider
    {
        /// <summary>
        ///     The names of elements supported by the provider.
        /// </summary>
        static readonly HashSet<string> SupportedElementNames = new HashSet<string>
        {
            "PackageReference",
            "DotNetCliToolReference"
        };

        /// <summary>
        ///     Create a new <see cref="PackageReferenceCompletion"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public PackageReferenceCompletion(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     The provider display name.
        /// </summary>
        public override string Name => "Package Reference Items";

        /// <summary>
        ///     The default sort priority for the provider's items.
        /// </summary>
        public override int Priority => 100;

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
        public override async Task<CompletionList> ProvideCompletions(XmlLocation location, ProjectDocument projectDocument, string triggerCharacters, CancellationToken cancellationToken = default)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (projectDocument == null)
                throw new ArgumentNullException(nameof(projectDocument));

            bool isIncomplete = false;
            List<CompletionItem> completions = new List<CompletionItem>();

            Log.Verbose("Evaluate completions for {XmlLocation:l}", location);

            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                if (location.CanCompleteAttributeValue(out XSAttribute attribute, WellKnownElementPaths.Item, "Include", "Version") && SupportedElementNames.Contains(attribute.Element.Name))
                {
                    Log.Verbose("Offering completions for value of attribute {AttributeName} of {ElementName} element @ {Position:l}",
                        attribute.Name,
                        attribute.Element.Name,
                        location.Position
                    );

                    List<CompletionItem> packageCompletions = await HandlePackageReferenceAttributeCompletion(projectDocument, attribute, cancellationToken);
                    if (packageCompletions != null)
                    {
                        isIncomplete |= packageCompletions.Count > 10; // Default page size.
                        completions.AddRange(packageCompletions);
                    }
                }
                else if (location.CanCompleteElement(out XSElement replaceElement, parentPath: WellKnownElementPaths.ItemGroup))
                {
                    Range targetRange;

                    if (replaceElement != null)
                    {
                        targetRange = replaceElement.Range;

                        Log.Verbose("Offering completions to replace child element @ {ReplaceRange} of {ElementName} @ {Position:l}",
                            targetRange,
                            "ItemGroup",
                            location.Position
                        );
                    }
                    else
                    {
                        targetRange = location.Position.ToEmptyRange();
                        
                        Log.Verbose("Offering completions for new child element of {ElementName} @ {Position:l}",
                            "ItemGroup",
                            targetRange
                        );
                    }

                    // Replace any characters that were typed to trigger the completion.
                    HandleTriggerCharacters(triggerCharacters, projectDocument, ref targetRange);

                    List<CompletionItem> elementCompletions = HandlePackageReferenceElementCompletion(location, projectDocument, targetRange);
                    if (elementCompletions != null)
                        completions.AddRange(elementCompletions);
                }
                else
                    Log.Verbose("Not offering any completions for {XmlLocation:l}", location);
            }

            Log.Verbose("Offering {CompletionCount} completions for {XmlLocation:l}", completions.Count, location);

            if (completions.Count == 0)
                return null;

            return new CompletionList(completions, isIncomplete);
        }

        /// <summary>
        ///     Handle completion for an attribute of a PackageReference element.
        /// </summary>
        /// <param name="projectDocument">
        ///     The current project document.
        /// </param>
        /// <param name="attribute">
        ///     The attribute for which completion is being requested.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task<List<CompletionItem>> HandlePackageReferenceAttributeCompletion(ProjectDocument projectDocument, XSAttribute attribute, CancellationToken cancellationToken)
        {
            bool includePreRelease = projectDocument.Workspace.Configuration.NuGet.IncludePreRelease;

            if (attribute.Name == "Include")
            {
                string packageIdPrefix = attribute.Value;
                SortedSet<string> packageIds = await projectDocument.SuggestPackageIds(packageIdPrefix, includePreRelease, cancellationToken);

                var completionItems = new List<CompletionItem>(
                    packageIds.Select((packageId, index) => new CompletionItem
                    {
                        Label = packageId,
                        Detail = "Package Id",
                        SortText = $"{Priority:0000}NuGet{index:000}",
                        Kind = CompletionItemKind.Module,
                        TextEdit = new TextEdit
                        {
                            Range = attribute.ValueRange.ToLsp(),
                            NewText = packageId
                        }
                    })
                );

                return completionItems;
            }

            if (attribute.Name == "Version")
            {
                XSAttribute includeAttribute = attribute.Element["Include"];
                if (includeAttribute == null)
                    return null;

                string packageId = includeAttribute.Value;
                IEnumerable<NuGetVersion> packageVersions = await projectDocument.SuggestPackageVersions(packageId, includePreRelease, cancellationToken);
                if (projectDocument.Workspace.Configuration.NuGet.ShowNewestVersionsFirst)
                    packageVersions = packageVersions.Reverse();

                LspModels.Range replacementRange = attribute.ValueRange.ToLsp();

                var completionItems = new List<CompletionItem>(
                    packageVersions.Select((packageVersion, index) => new CompletionItem
                    {
                        Label = packageVersion.ToNormalizedString(),
                        Detail = "Package Version",
                        SortText = $"{Priority:0000}NuGet{index:000}",
                        Kind = CompletionItemKind.Field,
                        TextEdit = new TextEdit
                        {
                            Range = replacementRange,
                            NewText = packageVersion.ToNormalizedString()
                        }
                    })
                );

                return completionItems;
            }

            // No completions.
            return null;
        }

        /// <summary>
        ///     Handle completion for an attribute of a PackageReference element.
        /// </summary>
        /// <param name="location">
        ///     The location where completion will be offered.
        /// </param>
        /// <param name="projectDocument">
        ///     The current project document.
        /// </param>
        /// <param name="replaceRange">
        ///     The range of text that will be replaced by the completion.
        /// </param>
        /// <returns>
        ///     The completion list or <c>null</c> if no completions are provided.
        /// </returns>
        List<CompletionItem> HandlePackageReferenceElementCompletion(XmlLocation location, ProjectDocument projectDocument, Range replaceRange)
        {
            if (projectDocument == null)
                throw new ArgumentNullException(nameof(projectDocument));

            if (location == null)
                throw new ArgumentNullException(nameof(location));

            return new List<CompletionItem>
            {
                new CompletionItem
                {
                    Label = "<PackageReference />",
                    Detail = "Element",
                    Documentation = "A NuGet package",
                    SortText = $"{Priority}A<PackageReference />",
                    Kind = CompletionItemKind.Class,
                    TextEdit = new TextEdit
                    {
                        NewText = "<PackageReference Include=\"${1:PackageId}\" Version=\"${2:PackageVersion}\" />$0",
                        Range = replaceRange.ToLsp()
                    },
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "<DotNetCliToolReference />",
                    Detail = "Element",
                    Documentation = "A command extension package for the dotnet CLI",
                    Kind = CompletionItemKind.Class,
                    SortText = $"{Priority}B<DotNetCliToolReference />",
                    TextEdit = new TextEdit
                    {
                        NewText = "<DotNetCliToolReference Include=\"${1:PackageId}\" Version=\"${2:PackageVersion}\" />$0",
                        Range = replaceRange.ToLsp()
                    },
                    InsertTextFormat = InsertTextFormat.Snippet
                }
            };
        }
    }
}
