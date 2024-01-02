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
    using SemanticModel.MSBuildExpressions;
    using Utilities;

    /// <summary>
    ///     Completion provider for property expressions.
    /// </summary>
    public class PropertyExpressionCompletionProvider
        : CompletionProvider
    {
        /// <summary>
        ///     Create a new <see cref="PropertyExpressionCompletionProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public PropertyExpressionCompletionProvider(ILogger logger)
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
                if (!projectDocument.EnableExpressions)
                    return null;

                if (!location.IsExpression(out ExpressionNode expression, out Range expressionRange))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (not on an expression or a location where an expression can be added).", location);

                    return null;
                }

                if (expression.Kind != ExpressionKind.Evaluate)
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (this provider only supports MSBuild Evaluation expressions, not {ExpressionKind} expressions).", location, expression.Kind);

                    return null;
                }

                Log.Verbose("Offering completions to replace Evaluate expression @ {ReplaceRange:l}",
                    expressionRange
                );

                completions.AddRange(
                    GetCompletionItems(projectDocument, expressionRange)
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

            // Well-known properties.
            foreach (string wellKnownPropertyName in MSBuildSchemaHelp.WellKnownPropertyNames)
            {
                if (!offeredPropertyNames.Add(wellKnownPropertyName))
                    continue;

                yield return PropertyCompletionItem(wellKnownPropertyName, replaceRangeLsp,
                    description: MSBuildSchemaHelp.ForProperty(wellKnownPropertyName)
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
                    description: "Property defined in this project (or a project it imports)."
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
        ///     An optional description for the item.
        /// </param>
        /// <returns>
        ///     The <see cref="CompletionItem"/>.
        /// </returns>
        CompletionItem PropertyCompletionItem(string propertyName, LspModels.Range replaceRange, int? priority = null, string description = null)
        {
            return new CompletionItem
            {
                Label = $"$({propertyName})",
                Detail = "Property",
                Documentation = description,
                SortText = $"{priority ?? Priority:0000}$({propertyName})",
                TextEdit = new TextEdit
                {
                    NewText = $"$({propertyName})",
                    Range = replaceRange
                }
            };
        }
    }
}
