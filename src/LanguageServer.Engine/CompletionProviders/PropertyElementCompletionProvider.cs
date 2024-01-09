using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Completion provider for the common property elements.
    /// </summary>
    public class PropertyElementCompletionProvider
        : CompletionProvider
    {
        /// <summary>
        ///     Create a new <see cref="PropertyElementCompletionProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public PropertyElementCompletionProvider(ILogger logger)
            : base(logger)
        {
        }

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

            var completions = new List<CompletionItem>();

            Log.Verbose("Evaluate completions for {XmlLocation:l}", location);

            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                if (!location.CanCompleteElement(out XSElement replaceElement, parentPath: WellKnownElementPaths.PropertyGroup))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (not a direct child of a 'PropertyGroup' element).", location);

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
                HandleTriggerCharacters(triggerCharacters, projectDocument, ref targetRange);

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
        ///     Get property element completions.
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
            LspModels.Range replaceRangeLsp = replaceRange.ToLsp();

            var offeredPropertyNames = new HashSet<string>();

            // Well-known (but standard-format) properties.

            foreach (string wellKnownPropertyName in MSBuildSchemaHelp.WellKnownPropertyNames)
            {
                if (!offeredPropertyNames.Add(wellKnownPropertyName))
                    continue;

                var defaultValues = MSBuildSchemaHelp.DefaultsForProperty(wellKnownPropertyName);

                yield return PropertyCompletionItem(wellKnownPropertyName, replaceRangeLsp,
                    description: MSBuildSchemaHelp.ForProperty(wellKnownPropertyName),
                    defaultValues: defaultValues
                );
            }

            if (!projectDocument.HasMSBuildProject)
                yield break; // Without a valid MSBuild project (even a cached one will do), we can't inspect existing MSBuild properties.

            if (!projectDocument.Workspace.Configuration.Language.CompletionsFromProject.Contains(CompletionSource.Property))
                yield break;

            int otherPropertyPriority = Priority + 10;

            string[] otherPropertyNames =
                projectDocument.MSBuildProject.Properties
                    .Select(property => property.Name)
                    .Where(propertyName => !propertyName.StartsWith("_")) // Ignore private properties.
                    .ToArray();
            foreach (string propertyName in otherPropertyNames)
            {
                if (!offeredPropertyNames.Add(propertyName))
                    continue;

                yield return PropertyCompletionItem(propertyName, replaceRangeLsp, otherPropertyPriority,
                    description: $"I don't know anything about the '{propertyName}' property, but it's defined in this project (or a project that it imports); you can override its value by specifying it here."
                );
            }
        }

        /// <summary>
        ///     Create a standard <see cref="CompletionItem"/> for the specified MSBuild property.
        /// </summary>
        /// <param name="propertyName">
        ///     The MSBuild property name.
        /// </param>
        /// <param name="replaceRange">
        ///     The range of text that will be replaced by the completion.
        /// </param>
        /// <param name="priority">
        ///     The item sort priority (defaults to <see cref="CompletionProvider.Priority"/>).
        /// </param>
        /// <param name="description">
        ///     An optional description for the property.
        /// </param>
        /// <param name="defaultValues">
        ///     An optional list of default values for the property.
        /// 
        ///     If specified, then the inserted property's snippet will offer these as a drop-down list.
        /// </param>
        /// <returns>
        ///     The <see cref="CompletionItem"/>.
        /// </returns>
        CompletionItem PropertyCompletionItem(string propertyName, LspModels.Range replaceRange, int? priority = null, string description = null, IReadOnlyList<string> defaultValues = null)
        {
            return new CompletionItem
            {
                Label = $"<{propertyName}>",
                Detail = "Property",
                Documentation = description,
                Kind = CompletionItemKind.Property,
                SortText = $"{priority ?? Priority:0000}<{propertyName}>",
                TextEdit = new TextEdit
                {
                    NewText = GetCompletionText(propertyName, defaultValues),
                    Range = replaceRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };
        }

        /// <summary>
        ///     Get the completion text for the specified property and its default value(s), if any.
        /// </summary>
        /// <param name="propertyName">
        ///     The property name.
        /// </param>
        /// <param name="defaultValues">
        ///     The property's default values (if any).
        /// 
        ///     If specified, then the inserted property's snippet will offer these as a drop-down list.
        /// </param>
        /// <returns>
        ///     The completion text (in standard LSP Snippet format).
        /// </returns>
        static string GetCompletionText(string propertyName, IReadOnlyList<string> defaultValues)
        {
            var completionText = new StringBuilder();
            completionText.AppendFormat("<{0}>", propertyName);

            bool haveValue = false;
            if (defaultValues is { Count: > 0 })
            {
                haveValue = true;

                completionText.Append("${1|");
                completionText.Append(
                    defaultValues[0]
                );
                for (int valueIndex = 1; valueIndex < defaultValues.Count; valueIndex++)
                {
                    completionText.Append(',');
                    completionText.Append(
                        defaultValues[valueIndex]
                    );
                }
                completionText.Append("|}");
            }
            else
            {
                completionText.Append("$0");
            }

            completionText.AppendFormat("</{0}>", propertyName);

            // If we have a default value / values, then the final cursor position should be after the property element.
            if (haveValue)
                completionText.Append("$0");

            return completionText.ToString();
        }
    }
}
