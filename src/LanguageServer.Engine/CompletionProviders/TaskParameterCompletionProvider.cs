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
    ///     Completion provider for the MSBuild task attributes.
    /// </summary>
    public class TaskParameterCompletionProvider
        : TaskCompletionProvider
    {
        /// <summary>
        ///     Create a new <see cref="TaskParameterCompletionProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public TaskParameterCompletionProvider(ILogger logger)
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
                Log.Verbose("Not offering task attribute completions for {XmlLocation:l} (underlying MSBuild project is not loaded).", location);

                return null;
            }

            var completions = new List<CompletionItem>();

            Log.Verbose("Evaluate completions for {XmlLocation:l}", location);

            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                if (!location.CanCompleteAttribute(out XSElement taskElement, out XSAttribute replaceAttribute, out PaddingType needsPadding))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (not a location an attribute can be created or replaced by completion).", location);

                    return null;
                }

                if (taskElement.ParentElement?.Name != "Target")
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (attribute is not on an element that's a direct child of a 'Target' element).", location);

                    return null;
                }

                Dictionary<string, MSBuildTaskMetadata> projectTasks = GetProjectTasks(projectDocument);
                if (!projectTasks.TryGetValue(taskElement.Name, out MSBuildTaskMetadata taskMetadata))
                {
                    Log.Verbose("Not offering any completions for {XmlLocation:l} (no metadata available for task {TaskName}).", location, taskElement.Name);

                    return null;
                }

                Range replaceRange = replaceAttribute?.Range ?? location.Position.ToEmptyRange();
                if (replaceAttribute != null)
                {
                    Log.Verbose("Offering completions to replace attribute {AttributeName} @ {ReplaceRange:l}",
                        replaceAttribute.Name,
                        replaceRange
                    );
                }
                else
                {
                    Log.Verbose("Offering completions to create attribute @ {ReplaceRange:l}",
                        replaceRange
                    );
                }

                var existingAttributeNames = new HashSet<string>(
                    taskElement.AttributeNames
                );
                if (replaceAttribute != null)
                    existingAttributeNames.Remove(replaceAttribute.Name);

                completions.AddRange(
                    GetCompletionItems(taskMetadata, existingAttributeNames, replaceRange, needsPadding)
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
        ///     Get task attribute completions.
        /// </summary>
        /// <param name="taskMetadata">
        ///     Metadata for the task whose parameters are being offered as completions.
        /// </param>
        /// <param name="existingAttributeNames">
        ///     Existing parameter names (if any) declared on the element.
        /// </param>
        /// <param name="replaceRange">
        ///     The range of text to be replaced by the completions.
        /// </param>
        /// <param name="needsPadding">
        ///     The type of padding (if any) required.
        /// </param>
        /// <returns>
        ///     A sequence of <see cref="CompletionItem"/>s.
        /// </returns>
        public IEnumerable<CompletionItem> GetCompletionItems(MSBuildTaskMetadata taskMetadata, HashSet<string> existingAttributeNames, Range replaceRange, PaddingType needsPadding)
        {
            LspModels.Range replaceRangeLsp = replaceRange.ToLsp();

            foreach (MSBuildTaskParameterMetadata taskParameter in taskMetadata.Parameters.OrderBy(parameter => parameter.Name))
            {
                if (existingAttributeNames.Contains(taskParameter.Name))
                    continue;

                if (taskParameter.IsOutput)
                    continue;

                string parameterDocumentation = MSBuildSchemaHelp.ForTaskParameter(taskMetadata.Name, taskParameter.Name);

                yield return TaskParameterCompletionItem(taskParameter, parameterDocumentation, replaceRangeLsp, needsPadding);
            }
        }

        /// <summary>
        ///     Create a <see cref="CompletionItem"/> for the specified MSBuild task parameter.
        /// </summary>
        /// <param name="parameterMetadata">
        ///     The MSBuild task's metadata.
        /// </param>
        /// <param name="parameterDocumentation">
        ///     Documentation (if available) for the task parameter.
        /// </param>
        /// <param name="replaceRange">
        ///     The range of text that will be replaced by the completion.
        /// </param>
        /// <param name="needsPadding">
        ///     The type of padding (if any) required.
        /// </param>
        /// <returns>
        ///     The <see cref="CompletionItem"/>.
        /// </returns>
        CompletionItem TaskParameterCompletionItem(MSBuildTaskParameterMetadata parameterMetadata, string parameterDocumentation, LspModels.Range replaceRange, PaddingType needsPadding)
        {
            return new CompletionItem
            {
                Label = parameterMetadata.Name,
                Detail = "Task Parameter",
                Documentation = parameterDocumentation,
                SortText = $"{Priority:0000}parameterMetadata.Name",
                TextEdit = new TextEdit
                {
                    NewText = $"{parameterMetadata.Name}=\"$1\"".WithPadding(needsPadding),
                    Range = replaceRange
                },
                InsertTextFormat = InsertTextFormat.Snippet
            };
        }
    }
}
