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
    ///     Completion provider for the MSBuild task elements.
    /// </summary>
    public class TaskElementCompletionProvider
        : TaskCompletionProvider
    {
        /// <summary>
        ///     Create a new <see cref="TaskElementCompletionProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public TaskElementCompletionProvider(ILogger logger)
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
            ArgumentNullException.ThrowIfNull(location);

            ArgumentNullException.ThrowIfNull(projectDocument);

            if (!projectDocument.HasMSBuildProject)
            {
                Log.Verbose("Not offering task element completions for {XmlLocation:l} (underlying MSBuild project is not loaded).", location);

                return null;
            }

            var completions = new List<CompletionItem>();

            Log.Verbose("Evaluate completions for {XmlLocation:l}", location);

            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                if (!location.CanCompleteElement(out XSElement replaceElement, parentPath: WellKnownElementPaths.Target))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (does not represent the direct child of a 'Target' element).", location);

                    return null;
                }

                Range targetRange = replaceElement?.Range ?? location.Position.ToEmptyRange();

                // Replace any characters that were typed to trigger the completion.
                HandleTriggerCharacters(triggerCharacters, projectDocument, ref targetRange);

                if (replaceElement != null)
                {
                    Log.Verbose("Offering completions to replace element {ElementName} @ {ReplaceRange:l}",
                        replaceElement.Name,
                        targetRange
                    );
                }
                else
                {
                    Log.Verbose("Offering completions to create task element @ {ReplaceRange:l}",
                        targetRange
                    );
                }

                Dictionary<string, MSBuildTaskMetadata> projectTasks = GetProjectTasks(projectDocument);

                completions.AddRange(
                    GetCompletionItems(projectTasks, targetRange)
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
        ///     Get task element completions.
        /// </summary>
        /// <param name="projectTasks">
        ///     The metadata for the tasks defined in the project (and its imports), keyed by task name.
        /// </param>
        /// <param name="replaceRange">
        ///     The range of text to be replaced by the completions.
        /// </param>
        /// <returns>
        ///     A sequence of <see cref="CompletionItem"/>s.
        /// </returns>
        public IEnumerable<CompletionItem> GetCompletionItems(Dictionary<string, MSBuildTaskMetadata> projectTasks, Range replaceRange)
        {
            LspModels.Range replaceRangeLsp = replaceRange.ToLsp();

            foreach (string taskName in projectTasks.Keys.OrderBy(name => name))
                yield return TaskElementCompletionItem(taskName, projectTasks[taskName], replaceRangeLsp);

            // TODO: Offer task names for inline and assembly-name-based tasks.
        }

        /// <summary>
        ///     Create a <see cref="CompletionItem"/> for the specified MSBuild task element.
        /// </summary>
        /// <param name="taskName">
        ///     The MSBuild task name.
        /// </param>
        /// <param name="taskMetadata">
        ///     The MSBuild task's metadata.
        /// </param>
        /// <param name="replaceRange">
        ///     The range of text that will be replaced by the completion.
        /// </param>
        /// <returns>
        ///     The <see cref="CompletionItem"/>.
        /// </returns>
        CompletionItem TaskElementCompletionItem(string taskName, MSBuildTaskMetadata taskMetadata, LspModels.Range replaceRange)
        {
            MSBuildTaskParameterMetadata[] requiredParameters = taskMetadata.Parameters.Where(parameter => parameter.IsRequired).ToArray();
            string requiredAttributes = string.Join(" ", requiredParameters.Select(
                (parameter, index) => $"{parameter.Name}=\"${index + 1}\""
            ));
            string attributePadding = (requiredAttributes.Length > 0) ? " " : string.Empty;

            string restOfElement = " />$0";
            if (taskMetadata.Parameters.Any(parameter => parameter.IsOutput))
            {
                // Create Outputs sub-element if there are any output parameters.
                restOfElement = $">\n\t${requiredParameters.Length + 1}\n</{taskName}>$0";
            }

            return new CompletionItem
            {
                Label = $"<{taskName}>",
                Detail = "Task",
                Documentation = MSBuildSchemaHelp.ForTask(taskName),
                Kind = CompletionItemKind.Function,
                SortText = $"{Priority:0000}<{taskName}>",
                TextEdit = new TextEdit
                {
                    NewText = $"<{taskName}{attributePadding}{requiredAttributes}{restOfElement}",
                    Range = replaceRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };
        }
    }
}
